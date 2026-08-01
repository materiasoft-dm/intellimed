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

        foreach (var allocation in dto.Allocations)
        {
            var invoice = await _context.Invoices.FirstOrDefaultAsync(i => i.Id == allocation.InvoiceId);
            if (invoice == null || invoice.ClinicId != dto.ClinicId)
                throw new InvalidOperationException($"Invoice {allocation.InvoiceId} was not found in this clinic.");

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
            PayerClientId = dto.PayerClientId,
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
        {
            var invoice = await _context.Invoices.FirstAsync(i => i.Id == invoiceId);
            invoice.AmountPaid = BillingMath.RoundMoney(await _context.ReceiptAllocations
                .Where(a => a.InvoiceId == invoiceId && a.AllocationType == AllocationType.Payment)
                .SumAsync(a => a.Amount));

            if (invoice.AmountPaid >= invoice.TotalAmount)
                invoice.Status = InvoiceStatus.Paid;
            else if (invoice.AmountPaid > 0)
                invoice.Status = InvoiceStatus.PartiallyPaid;
        }
        await _context.SaveChangesAsync();

        return receipt.Id;
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
