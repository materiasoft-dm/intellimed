using IntelliMed.Core.DTOs;
using IntelliMed.Core.Entities;
using IntelliMed.Core.Interfaces;
using IntelliMed.Infrastructure.Data;
using IntelliMed.Infrastructure.Mappers;
using IntelliMed.Infrastructure.Services;
using Microsoft.EntityFrameworkCore;

namespace IntelliMed.Infrastructure.Repositories;

public class InvoiceRepository : Repository<Invoice>, IInvoiceRepository
{
    private readonly IBillingCalculator _billingCalculator;
    private readonly IDerivedFeeCalculator _derivedFeeCalculator;

    public InvoiceRepository(AppDbContext context, IBillingCalculator billingCalculator, IDerivedFeeCalculator derivedFeeCalculator) : base(context)
    {
        _billingCalculator = billingCalculator;
        _derivedFeeCalculator = derivedFeeCalculator;
    }

    public async Task<InvoiceDto?> GetByIdAsync(int id)
    {
        var invoice = await _dbSet.FindAsync(id);
        return invoice == null ? null : EntityMapper.ToDto(invoice);
    }

    public async Task<InvoiceDto?> GetByIdWithDetailsAsync(int id)
    {
        var invoice = await _dbSet
            .Include(i => i.Client)
            .Include(i => i.Appointment)
            .Include(i => i.Practitioner)
            .Include(i => i.Items)
                .ThenInclude(item => item.BillingItem)
            .Include(i => i.Payments)
            .FirstOrDefaultAsync(i => i.Id == id);
        return invoice == null ? null : EntityMapper.ToDto(invoice);
    }

    public async Task<IEnumerable<InvoiceDto>> SearchAsync(InvoiceSearchDto search)
    {
        var query = BuildSearchQuery(search);
        var invoices = await query
            .Include(i => i.Client)
            .ToListAsync();
        return invoices.Select(EntityMapper.ToDto);
    }

    public async Task<(IEnumerable<InvoiceDto> Items, int TotalCount)> GetPagedAsync(InvoiceSearchDto search)
    {
        var query = BuildSearchQuery(search);
        var totalCount = await query.CountAsync();

        var invoices = await query
            .Include(i => i.Client)
            .OrderByDescending(i => i.InvoiceDate)
            .Skip((search.Page - 1) * search.PageSize)
            .Take(search.PageSize)
            .ToListAsync();

        return (invoices.Select(EntityMapper.ToDto), totalCount);
    }

    public async Task<IEnumerable<InvoiceDto>> GetByClientIdAsync(int clientId)
    {
        var invoices = await _dbSet
            .Include(i => i.Client)
            .Where(i => i.ClientId == clientId)
            .OrderByDescending(i => i.InvoiceDate)
            .ToListAsync();
        return invoices.Select(EntityMapper.ToDto);
    }

    public async Task<IEnumerable<InvoiceDto>> GetOverdueInvoicesAsync()
    {
        var today = DateTime.UtcNow.Date;
        var invoices = await _dbSet
            .Include(i => i.Client)
            .Where(i => i.DueDate < today && i.Status != InvoiceStatus.Paid && i.Status != InvoiceStatus.Cancelled)
            .OrderBy(i => i.DueDate)
            .ToListAsync();
        return invoices.Select(EntityMapper.ToDto);
    }

    public async Task<string> GenerateInvoiceNumberAsync()
    {
        var year = DateTime.UtcNow.Year;
        var prefix = $"INV-{year}-";

        var lastInvoice = await _dbSet
            .Where(i => i.InvoiceNumber.StartsWith(prefix))
            .OrderByDescending(i => i.InvoiceNumber)
            .FirstOrDefaultAsync();

        int nextNumber = 1;
        if (lastInvoice != null)
        {
            var lastNumberStr = lastInvoice.InvoiceNumber.Replace(prefix, "");
            if (int.TryParse(lastNumberStr, out var lastNumber))
            {
                nextNumber = lastNumber + 1;
            }
        }

        return $"{prefix}{nextNumber:D5}";
    }

    public async Task<int> CreateAsync(CreateInvoiceDto dto)
    {
        var invoiceNumber = await GenerateInvoiceNumberAsync();
        var invoice = EntityMapper.ToEntity(dto, invoiceNumber);

        // Provider service-type drives the bulk-bill-equivalent rebate lookup (defaults to GP).
        var providerServiceType = ProviderServiceType.GeneralPractitioner;
        if (dto.PractitionerId.HasValue)
        {
            providerServiceType = await _context.Practitioners
                .Where(p => p.Id == dto.PractitionerId.Value)
                .Select(p => p.ServiceType)
                .FirstOrDefaultAsync();
        }

        // The client's own HealthFundId (not anything client-supplied on the DTO) is the single
        // source of truth for fund-specific fee schedule resolution.
        var healthFundId = await _context.Clients
            .Where(c => c.Id == dto.ClientId)
            .Select(c => c.HealthFundId)
            .FirstOrDefaultAsync();

        // The charged fee (UnitPrice) is user-editable, so we keep whatever the client sent; the
        // rebate/GST are derived authoritatively from the billing context.
        foreach (var item in invoice.Items.Where(i => i.BillingItemId.HasValue))
        {
            var resolved = await _billingCalculator.ResolveLineAsync(
                dto.ClinicId, dto.AccountType, providerServiceType, dto.PlaceOfService,
                item.BillingItemId!.Value, item.ServiceDate, healthFundId);

            item.RebatePerUnit = resolved.RebatePerUnit;
            item.GstAmount = resolved.GstAmount;
            item.PercentGst = resolved.PercentGst;
            if (string.IsNullOrWhiteSpace(item.Description))
                item.Description = resolved.Description;
        }

        // Derived items (assistant-at-surgery, patients-seen, time-loading, etc.) need every line's
        // normally-resolved fee already in place before their own formulas can run, and must run
        // before the invoice total is summed so the override is reflected in TotalAmount.
        await _derivedFeeCalculator.ApplyDerivedFeesAsync(invoice.Items);

        invoice.TotalAmount = BillingMath.RoundMoney(invoice.Items.Sum(i => i.TotalPrice));

        await _dbSet.AddAsync(invoice);
        await _context.SaveChangesAsync();
        return invoice.Id;
    }

    public async Task<ResolveLineResult> ResolveLineAsync(ResolveLineRequest request)
    {
        var providerServiceType = ProviderServiceType.GeneralPractitioner;
        if (request.PractitionerId.HasValue)
        {
            providerServiceType = await _context.Practitioners
                .Where(p => p.Id == request.PractitionerId.Value)
                .Select(p => p.ServiceType)
                .FirstOrDefaultAsync();
        }

        return await _billingCalculator.ResolveLineAsync(
            request.ClinicId, request.AccountType, providerServiceType, request.PlaceOfService,
            request.BillingItemId, request.ServiceDate, request.HealthFundId);
    }

    public async Task AddPaymentAsync(int invoiceId, CreatePaymentDto dto)
    {
        var invoice = await _dbSet
            .Include(i => i.Payments)
            .FirstOrDefaultAsync(i => i.Id == invoiceId);

        if (invoice == null)
            throw new InvalidOperationException($"Invoice with ID {invoiceId} not found");

        var payment = new Payment
        {
            InvoiceId = invoiceId,
            Amount = dto.Amount,
            PaymentDate = dto.PaymentDate,
            Method = dto.Method,
            Reference = dto.Reference,
            CreatedAt = DateTime.UtcNow
        };

        await _context.Payments.AddAsync(payment);

        // Update invoice total paid
        invoice.AmountPaid += dto.Amount;

        // Check if fully paid
        if (invoice.AmountPaid >= invoice.TotalAmount)
        {
            invoice.Status = InvoiceStatus.Paid;
        }
        else if (invoice.AmountPaid > 0)
        {
            invoice.Status = InvoiceStatus.PartiallyPaid;
        }

        await _context.SaveChangesAsync();
    }

    public async Task UpdateStatusAsync(int id, InvoiceStatus status)
    {
        var invoice = await _dbSet.FindAsync(id);
        if (invoice == null)
            throw new InvalidOperationException($"Invoice with ID {id} not found");

        invoice.Status = status;
        invoice.UpdatedAt = DateTime.UtcNow;
        await _context.SaveChangesAsync();
    }

    public async Task<(IEnumerable<PaymentDto> Items, int TotalCount)> GetAllPaymentsAsync(PaymentSearchDto search)
    {
        var query = _context.Payments
            .Include(p => p.Invoice)
            .ThenInclude(i => i!.Client)
            .AsQueryable();

        if (search.ClinicId.HasValue)
            query = query.Where(p => p.Invoice!.ClinicId == search.ClinicId.Value);

        if (search.Method.HasValue)
            query = query.Where(p => p.Method == search.Method.Value);

        if (search.FromDate.HasValue)
            query = query.Where(p => p.PaymentDate >= search.FromDate.Value);

        if (search.ToDate.HasValue)
            query = query.Where(p => p.PaymentDate <= search.ToDate.Value);

        var totalCount = await query.CountAsync();

        var payments = await query
            .OrderByDescending(p => p.PaymentDate)
            .Skip((search.Page - 1) * search.PageSize)
            .Take(search.PageSize)
            .ToListAsync();

        return (payments.Select(EntityMapper.ToDto), totalCount);
    }

    private IQueryable<Invoice> BuildSearchQuery(InvoiceSearchDto search)
    {
        var query = _dbSet.AsQueryable();

        if (search.ClinicId.HasValue)
            query = query.Where(i => i.ClinicId == search.ClinicId.Value);

        if (search.ClientId.HasValue)
            query = query.Where(i => i.ClientId == search.ClientId.Value);

        if (search.Status.HasValue)
            query = query.Where(i => i.Status == search.Status.Value);

        if (search.FromDate.HasValue)
            query = query.Where(i => i.InvoiceDate >= search.FromDate.Value);

        if (search.ToDate.HasValue)
            query = query.Where(i => i.InvoiceDate <= search.ToDate.Value);

        return query;
    }
}
