using IntelliMed.Core.DTOs;
using IntelliMed.Core.Entities;
using IntelliMed.Core.Interfaces;
using IntelliMed.Infrastructure.Data;
using IntelliMed.Infrastructure.Mappers;
using IntelliMed.Infrastructure.Services;
using Microsoft.EntityFrameworkCore;

namespace IntelliMed.Infrastructure.Repositories;

public class ReceiptRepository : Repository<Receipt>, IReceiptRepository
{
    public ReceiptRepository(AppDbContext context) : base(context)
    {
    }

    public async Task<ReceiptDto?> GetByIdAsync(int id)
    {
        var receipt = await _dbSet
            .Include(r => r.PayerClient)
            .Include(r => r.Payments)
            .Include(r => r.Allocations)
                .ThenInclude(a => a.Invoice)
            .Include(r => r.Allocations)
                .ThenInclude(a => a.InvoiceItem)
            .FirstOrDefaultAsync(r => r.Id == id);
        if (receipt == null) return null;

        var clinic = await _context.Clinics.AsNoTracking().FirstOrDefaultAsync(c => c.Id == receipt.ClinicId);
        return EntityMapper.ToDto(receipt, clinic);
    }

    public async Task<int> CreateAsync(CreateReceiptDto dto)
    {
        var tenderTotal = BillingMath.RoundMoney(dto.Payments.Sum(t => t.Amount));
        var allocationTotal = BillingMath.RoundMoney(dto.Allocations.Sum(a => a.Amount));

        if (dto.Payments.Count == 0)
            throw new InvalidOperationException("A receipt must have at least one payment.");
        if (dto.Payments.Any(t => t.Amount <= 0) || dto.Allocations.Any(a => a.Amount <= 0))
            throw new InvalidOperationException("Payment and allocation amounts must be greater than zero.");
        if (tenderTotal < allocationTotal)
            throw new InvalidOperationException("Tendered amount is less than the amount being allocated.");

        // Re-derived from the target invoice(s) rather than trusted from the DTO — matches
        // InvoiceRepository.AddPaymentAsync's existing pattern. A receipt spanning multiple invoices
        // must resolve to the same payer for all of them.
        int? resolvedPayerClientId = null;
        foreach (var allocation in dto.Allocations)
        {
            var invoice = await _context.Invoices.Include(i => i.Client).FirstOrDefaultAsync(i => i.Id == allocation.InvoiceId);
            if (invoice == null || invoice.ClinicId != dto.ClinicId)
                throw new InvalidOperationException($"Invoice {allocation.InvoiceId} was not found in this clinic.");

            var invoicePayerClientId = invoice.Client?.PayerClientId ?? invoice.ClientId;
            if (resolvedPayerClientId.HasValue && resolvedPayerClientId.Value != invoicePayerClientId)
                throw new InvalidOperationException("All allocations in a receipt must belong to the same payer.");
            resolvedPayerClientId = invoicePayerClientId;

            if (allocation.InvoiceItemId.HasValue)
            {
                var itemBelongs = await _context.InvoiceItems.AnyAsync(
                    i => i.Id == allocation.InvoiceItemId.Value && i.InvoiceId == allocation.InvoiceId);
                if (!itemBelongs)
                    throw new InvalidOperationException($"Invoice item {allocation.InvoiceItemId} does not belong to invoice {allocation.InvoiceId}.");
            }
        }

        var receipt = new Receipt
        {
            ClinicId = dto.ClinicId,
            // Falls back to the client-supplied value only when there are no allocations to derive
            // a payer from (nothing currently calls CreateAsync that way, but it's not hard-blocked).
            PayerClientId = resolvedPayerClientId ?? dto.PayerClientId,
            ReceiptDate = dto.ReceiptDate,
            Notes = dto.Notes,
            CreatedAt = DateTime.UtcNow,
            Payments = dto.Payments.Select(t => new ReceiptPayment
            {
                Amount = t.Amount,
                Method = t.Method,
                Reference = t.Reference,
                CreatedAt = DateTime.UtcNow
            }).ToList(),
            Allocations = dto.Allocations.Select(a => new ReceiptAllocation
            {
                InvoiceId = a.InvoiceId,
                InvoiceItemId = a.InvoiceItemId,
                Amount = a.Amount,
                AllocationType = AllocationType.Payment,
                CreatedAt = DateTime.UtcNow
            }).ToList()
        };

        var overpayment = tenderTotal - allocationTotal;
        if (overpayment > 0)
        {
            if (!dto.KeepOverpaymentAsCredit)
                throw new InvalidOperationException($"Tendered amount exceeds allocations by {overpayment:C}.");

            receipt.Allocations.Add(new ReceiptAllocation
            {
                // InvoiceId/InvoiceItemId left null — this is the payer-level-credit meaning.
                Amount = overpayment,
                AllocationType = AllocationType.Credit,
                CreatedAt = DateTime.UtcNow
            });
        }

        await _dbSet.AddAsync(receipt);
        await _context.SaveChangesAsync();

        foreach (var invoiceId in dto.Allocations.Select(a => a.InvoiceId).Distinct())
            await RecomputeInvoiceAmountsAsync(invoiceId);
        await _context.SaveChangesAsync();

        return receipt.Id;
    }

    public async Task<int> CreateRefundAsync(CreateRefundDto dto)
    {
        if (dto.Amount <= 0)
            throw new InvalidOperationException("Refund amount must be greater than zero.");
        if (string.IsNullOrWhiteSpace(dto.Reason))
            throw new InvalidOperationException("A reason is required to record a refund.");

        // Re-derived from the target invoice when one is set, same as CreateAsync — see its comment.
        // A payer-level credit refund (InvoiceId == null) has no invoice to derive from, so
        // dto.PayerClientId stays trusted as-is in that one case; no alternative source of truth exists.
        int? resolvedPayerClientId = null;
        if (dto.InvoiceId.HasValue)
        {
            var invoice = await _context.Invoices.Include(i => i.Client).FirstOrDefaultAsync(i => i.Id == dto.InvoiceId.Value);
            if (invoice == null || invoice.ClinicId != dto.ClinicId)
                throw new InvalidOperationException($"Invoice {dto.InvoiceId} was not found in this clinic.");
            if (invoice.Status == InvoiceStatus.Cancelled)
                throw new InvalidOperationException("Cannot refund a cancelled invoice.");

            // AmountPaid is already net of every prior refund (RecomputeInvoiceAmountsAsync computes
            // it as SUM(Payment allocations) - SUM(Refund allocations)), so it IS the refundable cap —
            // subtracting prior refunds again here would double-count them.
            if (dto.Amount > invoice.AmountPaid)
                throw new InvalidOperationException($"Refund amount exceeds the refundable balance of {invoice.AmountPaid:C}.");

            resolvedPayerClientId = invoice.Client?.PayerClientId ?? invoice.ClientId;
        }
        else
        {
            var available = await GetAvailableCreditAsync(dto.ClinicId, dto.PayerClientId);
            if (dto.Amount > available)
                throw new InvalidOperationException($"Refund amount exceeds the available credit balance of {available:C}.");
        }

        var receipt = new Receipt
        {
            ClinicId = dto.ClinicId,
            PayerClientId = resolvedPayerClientId ?? dto.PayerClientId,
            ReceiptDate = dto.RefundDate,
            Notes = dto.Reason,
            CreatedAt = DateTime.UtcNow,
            Payments = new List<ReceiptPayment>
            {
                new() { Amount = dto.Amount, Method = dto.Method, Reference = dto.Reference, CreatedAt = DateTime.UtcNow }
            },
            Allocations = new List<ReceiptAllocation>
            {
                new()
                {
                    InvoiceId = dto.InvoiceId,
                    InvoiceItemId = null,
                    Amount = dto.Amount,
                    AllocationType = AllocationType.Refund,
                    CreatedAt = DateTime.UtcNow
                }
            }
        };

        await _dbSet.AddAsync(receipt);
        await _context.SaveChangesAsync();

        if (dto.InvoiceId.HasValue)
        {
            await RecomputeInvoiceAmountsAsync(dto.InvoiceId.Value);
            await _context.SaveChangesAsync();
        }

        return receipt.Id;
    }

    public async Task<decimal> GetAvailableCreditAsync(int clinicId, int payerClientId)
    {
        var credit = await _context.ReceiptAllocations
            .Where(a => a.Receipt!.ClinicId == clinicId && a.Receipt.PayerClientId == payerClientId
                && a.InvoiceId == null && a.AllocationType == AllocationType.Credit)
            .SumAsync(a => a.Amount);
        var refunded = await _context.ReceiptAllocations
            .Where(a => a.Receipt!.ClinicId == clinicId && a.Receipt.PayerClientId == payerClientId
                && a.InvoiceId == null && a.AllocationType == AllocationType.Refund)
            .SumAsync(a => a.Amount);

        return BillingMath.RoundMoney(credit - refunded);
    }

    /// <summary>Recomputes AmountPaid/Status for one invoice from its ReceiptAllocations — shared by
    /// CreateAsync (Payment allocations) and CreateRefundAsync (Refund allocations net against them).</summary>
    private async Task RecomputeInvoiceAmountsAsync(int invoiceId)
    {
        var invoice = await _context.Invoices.FirstAsync(i => i.Id == invoiceId);

        var paid = await _context.ReceiptAllocations
            .Where(a => a.InvoiceId == invoiceId && a.AllocationType == AllocationType.Payment)
            .SumAsync(a => a.Amount);
        var refunded = await _context.ReceiptAllocations
            .Where(a => a.InvoiceId == invoiceId && a.AllocationType == AllocationType.Refund)
            .SumAsync(a => a.Amount);
        invoice.AmountPaid = BillingMath.RoundMoney(paid - refunded);

        if (invoice.AmountPaid >= invoice.TotalAmount && invoice.AmountPaid > 0)
            invoice.Status = InvoiceStatus.Paid;
        else if (invoice.AmountPaid > 0)
            invoice.Status = InvoiceStatus.PartiallyPaid;
        else
            invoice.Status = InvoiceStatus.Sent;
    }

    public async Task<List<OutstandingInvoiceDto>> GetOutstandingInvoicesForPayerAsync(int clinicId, int payerClientId, int? excludeInvoiceId = null)
    {
        var invoices = await _context.Invoices
            .Include(i => i.Client)
            .Where(i => i.ClinicId == clinicId
                && i.Status != InvoiceStatus.Cancelled
                && (i.Client!.PayerClientId == payerClientId || (i.Client.PayerClientId == null && i.ClientId == payerClientId))
                && (excludeInvoiceId == null || i.Id != excludeInvoiceId))
            .ToListAsync();

        return invoices
            .Select(i => new OutstandingInvoiceDto
            {
                InvoiceId = i.Id,
                InvoiceNumber = i.InvoiceNumber,
                ClientName = i.Client != null ? $"{i.Client.FirstName} {i.Client.LastName}" : string.Empty,
                InvoiceDate = i.InvoiceDate,
                TotalAmount = i.TotalAmount,
                AmountOwing = i.AmountOwing
            })
            .Where(d => d.AmountOwing > 0)
            .OrderBy(d => d.InvoiceDate)
            .ToList();
    }
}
