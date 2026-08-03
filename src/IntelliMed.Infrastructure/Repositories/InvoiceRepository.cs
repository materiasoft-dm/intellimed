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
    private readonly IMultipleOperationRuleCalculator _multipleOperationRuleCalculator;
    private readonly IReceiptRepository _receiptRepository;

    public InvoiceRepository(
        AppDbContext context,
        IBillingCalculator billingCalculator,
        IDerivedFeeCalculator derivedFeeCalculator,
        IMultipleOperationRuleCalculator multipleOperationRuleCalculator,
        IReceiptRepository receiptRepository) : base(context)
    {
        _billingCalculator = billingCalculator;
        _derivedFeeCalculator = derivedFeeCalculator;
        _multipleOperationRuleCalculator = multipleOperationRuleCalculator;
        _receiptRepository = receiptRepository;
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
            .Include(i => i.Items)
                .ThenInclude(item => item.FeeSchedule)
            .Include(i => i.ReceiptAllocations)
                .ThenInclude(a => a.Receipt)
                    .ThenInclude(r => r!.Payments)
            .Include(i => i.WriteOffs)
            .FirstOrDefaultAsync(i => i.Id == id);
        if (invoice == null) return null;

        var clinic = await _context.Clinics.AsNoTracking().FirstOrDefaultAsync(c => c.Id == invoice.ClinicId);
        return EntityMapper.ToDto(invoice, clinic);
    }

    public async Task<IEnumerable<InvoiceDto>> SearchAsync(InvoiceSearchDto search)
    {
        var query = BuildSearchQuery(search);
        var invoices = await query
            .Include(i => i.Client)
            .ToListAsync();
        return invoices.Select(i => EntityMapper.ToDto(i));
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

        return (invoices.Select(i => EntityMapper.ToDto(i)), totalCount);
    }

    public async Task<IEnumerable<InvoiceDto>> GetByClientIdAsync(int clientId)
    {
        var invoices = await _dbSet
            .Include(i => i.Client)
            .Where(i => i.ClientId == clientId)
            .OrderByDescending(i => i.InvoiceDate)
            .ToListAsync();
        return invoices.Select(i => EntityMapper.ToDto(i));
    }

    public async Task<IEnumerable<InvoiceDto>> GetOverdueInvoicesAsync()
    {
        var today = DateTime.UtcNow.Date;
        var invoices = await _dbSet
            .Include(i => i.Client)
            .Where(i => i.DueDate < today && i.Status != InvoiceStatus.Paid && i.Status != InvoiceStatus.Cancelled)
            .OrderBy(i => i.DueDate)
            .ToListAsync();
        return invoices.Select(i => EntityMapper.ToDto(i));
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
                item.BillingItemId!.Value, item.ServiceDate, healthFundId, item.FeeScheduleId);

            item.RebatePerUnit = resolved.RebatePerUnit;
            item.GstAmount = resolved.GstAmount;
            item.PercentGst = resolved.PercentGst;
            if (string.IsNullOrWhiteSpace(item.Description))
                item.Description = resolved.Description;
        }

        // The Multiple Operation Rule abates 2nd/3rd+ same-occasion Group T8 procedures before
        // anything derived from their fee runs (e.g. an assistant-at-surgery fee is a percentage of
        // the *abated* fee, not the raw one) — so it must run before the derived-item pass below.
        await _multipleOperationRuleCalculator.ApplyMultipleOperationRuleAsync(invoice.Items, dto.AccountType);

        // Derived items (assistant-at-surgery, patients-seen, time-loading, etc.) need every line's
        // normally-resolved (and now abated) fee already in place before their own formulas can run,
        // and must run before the invoice total is summed so the override is reflected in TotalAmount.
        // The fee schedule is resolved separately (not per-line) since it's needed for per-schedule
        // derived-fee overrides (MedicalGapPercent etc.), not for a specific billing item.
        var feeScheduleId = await _billingCalculator.ResolveFeeScheduleIdAsync(dto.ClinicId, dto.AccountType, healthFundId);
        await _derivedFeeCalculator.ApplyDerivedFeesAsync(invoice.Items, feeScheduleId);

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
            request.BillingItemId, request.ServiceDate, request.HealthFundId, request.FeeScheduleId);
    }

    public async Task AddPaymentAsync(int invoiceId, CreatePaymentDto dto)
    {
        var invoice = await _dbSet
            .Include(i => i.Client)
            .FirstOrDefaultAsync(i => i.Id == invoiceId);

        if (invoice == null)
            throw new InvalidOperationException($"Invoice with ID {invoiceId} not found");

        // A thin single-invoice/single-tender adapter over the general receipting model — this is
        // what keeps AddInvoice.razor's "pay now" checkbox and any other simple caller working
        // unchanged while every payment, here or from the richer receipt UI, lands in the same place.
        await _receiptRepository.CreateAsync(new CreateReceiptDto
        {
            ClinicId = invoice.ClinicId,
            PayerClientId = invoice.Client?.PayerClientId ?? invoice.ClientId,
            ReceiptDate = dto.PaymentDate,
            Payments = new List<CreateReceiptTenderDto>
            {
                new() { Amount = dto.Amount, Method = dto.Method, Reference = dto.Reference }
            },
            Allocations = new List<CreateReceiptAllocationDto>
            {
                new() { InvoiceId = invoiceId, Amount = dto.Amount }
            },
            KeepOverpaymentAsCredit = true
        });
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
        var query = _context.ReceiptAllocations
            .Where(a => a.AllocationType == AllocationType.Payment && a.InvoiceId != null)
            .Include(a => a.Invoice)
                .ThenInclude(i => i!.Client)
            .Include(a => a.Receipt)
                .ThenInclude(r => r!.Payments)
            .AsQueryable();

        if (search.ClinicId.HasValue)
            query = query.Where(a => a.Invoice!.ClinicId == search.ClinicId.Value);

        // Matches if ANY tender on the receipt used this method — a split-tender receipt no longer
        // silently falls out of a method filter the way a flat one-method-per-row model would.
        if (search.Method.HasValue)
            query = query.Where(a => a.Receipt!.Payments.Any(p => p.Method == search.Method.Value));

        if (search.FromDate.HasValue)
            query = query.Where(a => a.Receipt!.ReceiptDate >= search.FromDate.Value);

        if (search.ToDate.HasValue)
            query = query.Where(a => a.Receipt!.ReceiptDate <= search.ToDate.Value);

        var totalCount = await query.CountAsync();

        var allocations = await query
            .OrderByDescending(a => a.Receipt!.ReceiptDate)
            .Skip((search.Page - 1) * search.PageSize)
            .Take(search.PageSize)
            .ToListAsync();

        return (allocations.Select(EntityMapper.ToPaymentDto), totalCount);
    }

    public async Task WriteOffAsync(int invoiceId, CreateWriteOffDto dto)
    {
        var invoice = await _dbSet.Include(i => i.WriteOffs).FirstOrDefaultAsync(i => i.Id == invoiceId);
        if (invoice == null)
            throw new InvalidOperationException($"Invoice with ID {invoiceId} not found");
        if (invoice.Status == InvoiceStatus.Cancelled)
            throw new InvalidOperationException("Cannot write off a cancelled invoice.");
        if (dto.Amount <= 0)
            throw new InvalidOperationException("Write-off amount must be greater than zero.");
        if (dto.Amount > invoice.AmountOwing)
            throw new InvalidOperationException($"Write-off amount exceeds the outstanding balance of {invoice.AmountOwing:C}.");
        if (string.IsNullOrWhiteSpace(dto.Reason))
            throw new InvalidOperationException("A reason is required to write off an invoice.");

        invoice.WriteOffs.Add(new InvoiceWriteOff
        {
            Amount = dto.Amount,
            Reason = dto.Reason,
            CreatedAt = DateTime.UtcNow
        });
        // Recomputed from the collection (not just added) so repeated partial write-offs accumulate correctly.
        invoice.AmountWrittenOff = BillingMath.RoundMoney(invoice.WriteOffs.Sum(w => w.Amount));
        invoice.UpdatedAt = DateTime.UtcNow;

        await _context.SaveChangesAsync();
    }

    public async Task CancelAsync(int invoiceId, CancelInvoiceDto dto)
    {
        var invoice = await _dbSet.FindAsync(invoiceId);
        if (invoice == null)
            throw new InvalidOperationException($"Invoice with ID {invoiceId} not found");
        if (invoice.Status == InvoiceStatus.Cancelled)
            throw new InvalidOperationException("This invoice is already cancelled.");
        if (invoice.AmountPaid > 0)
            throw new InvalidOperationException("Cannot cancel an invoice with payments — refund it first.");
        if (string.IsNullOrWhiteSpace(dto.Reason))
            throw new InvalidOperationException("A reason is required to cancel an invoice.");

        invoice.Status = InvoiceStatus.Cancelled;
        invoice.CancelReason = dto.Reason;
        invoice.UpdatedAt = DateTime.UtcNow;

        await _context.SaveChangesAsync();
    }

    public async Task<SplitInvoiceResultDto> SplitAsync(int sourceInvoiceId, SplitInvoiceDto dto)
    {
        var source = await _dbSet.Include(i => i.Items).FirstOrDefaultAsync(i => i.Id == sourceInvoiceId);
        if (source == null)
            throw new InvalidOperationException($"Invoice with ID {sourceInvoiceId} not found");
        if (source.Status == InvoiceStatus.Cancelled)
            throw new InvalidOperationException("Cannot split a cancelled invoice.");
        // Whole-invoice gate, not per-item: a payment recorded at invoice level (the only mode any
        // current UI creates) can't be traced back to a specific item, so partial movability can't be
        // determined safely once any money has moved.
        if (source.AmountPaid > 0 || source.AmountWrittenOff > 0)
            throw new InvalidOperationException("Cannot split an invoice that has payments or write-offs — refund or reverse them first.");
        if (string.IsNullOrWhiteSpace(dto.Reason))
            throw new InvalidOperationException("A reason is required to split an invoice.");
        if (dto.NewInvoices == null || dto.NewInvoices.Count == 0)
            throw new InvalidOperationException("At least one new invoice group is required.");

        var sourceItemIds = source.Items.Select(i => i.Id).ToHashSet();
        var claimedItemIds = new HashSet<int>();
        foreach (var group in dto.NewInvoices)
        {
            if (group.InvoiceItemIds == null || group.InvoiceItemIds.Count == 0)
                throw new InvalidOperationException("Each new invoice must receive at least one item.");

            foreach (var itemId in group.InvoiceItemIds)
            {
                if (!sourceItemIds.Contains(itemId))
                    throw new InvalidOperationException($"Invoice item {itemId} does not belong to invoice {sourceInvoiceId}.");
                if (!claimedItemIds.Add(itemId))
                    throw new InvalidOperationException($"Invoice item {itemId} was assigned to more than one new invoice.");
            }
        }

        if (claimedItemIds.Count >= sourceItemIds.Count)
            throw new InvalidOperationException("The source invoice must retain at least one item.");

        // A genuine multi-step mutation (several new invoices + item deletions + source total
        // recompute) where a partial failure would leave real damage — this codebase's first use of
        // an explicit transaction, used deliberately rather than reflexively. Guarded by IsRelational
        // since the EF InMemory provider (used by the repository test suite) doesn't support transactions.
        var transaction = _context.Database.IsRelational()
            ? await _context.Database.BeginTransactionAsync()
            : null;
        await using var _ = transaction;

        var newInvoiceIds = new List<int>();
        foreach (var group in dto.NewInvoices)
        {
            // GenerateInvoiceNumberAsync queries the DB directly, so each sibling must be saved
            // before the next number is generated or two siblings could collide on the same number.
            var invoiceNumber = await GenerateInvoiceNumberAsync();
            var newInvoice = EntityMapper.CloneInvoiceHeader(source, invoiceNumber);
            if (group.DueDate.HasValue)
                newInvoice.DueDate = group.DueDate.Value;

            foreach (var itemId in group.InvoiceItemIds)
            {
                var sourceItem = source.Items.First(i => i.Id == itemId);
                newInvoice.Items.Add(EntityMapper.CloneForSplit(sourceItem));
            }
            newInvoice.TotalAmount = BillingMath.RoundMoney(newInvoice.Items.Sum(i => i.TotalPrice));

            await _dbSet.AddAsync(newInvoice);
            await _context.SaveChangesAsync();
            newInvoiceIds.Add(newInvoice.Id);
        }

        var movedItems = source.Items.Where(i => claimedItemIds.Contains(i.Id)).ToList();
        _context.InvoiceItems.RemoveRange(movedItems);
        source.TotalAmount = BillingMath.RoundMoney(source.Items.Where(i => !claimedItemIds.Contains(i.Id)).Sum(i => i.TotalPrice));
        source.Notes = string.IsNullOrWhiteSpace(source.Notes)
            ? $"Split on {DateTime.UtcNow:yyyy-MM-dd}: {dto.Reason}"
            : $"{source.Notes}\nSplit on {DateTime.UtcNow:yyyy-MM-dd}: {dto.Reason}";
        source.UpdatedAt = DateTime.UtcNow;
        await _context.SaveChangesAsync();

        if (transaction != null)
            await transaction.CommitAsync();

        return new SplitInvoiceResultDto { SourceInvoiceId = sourceInvoiceId, NewInvoiceIds = newInvoiceIds };
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
