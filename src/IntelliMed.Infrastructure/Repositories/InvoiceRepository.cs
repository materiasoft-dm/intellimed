using IntelliMed.Core.DTOs;
using IntelliMed.Core.Entities;
using IntelliMed.Core.Interfaces;
using IntelliMed.Infrastructure.Data;
using IntelliMed.Infrastructure.Mappers;
using Microsoft.EntityFrameworkCore;

namespace IntelliMed.Infrastructure.Repositories;

public class InvoiceRepository : Repository<Invoice>, IInvoiceRepository
{
    public InvoiceRepository(AppDbContext context) : base(context)
    {
    }

    public async Task<InvoiceDto?> GetByIdAsync(int id)
    {
        var invoice = await _dbSet.FindAsync(id);
        return invoice == null ? null : EntityMapper.ToDto(invoice);
    }

    public async Task<InvoiceDto?> GetByIdWithDetailsAsync(int id)
    {
        var invoice = await _dbSet
            .Include(i => i.Patient)
            .Include(i => i.Appointment)
            .Include(i => i.Items)
            .Include(i => i.Payments)
            .FirstOrDefaultAsync(i => i.Id == id);
        return invoice == null ? null : EntityMapper.ToDto(invoice);
    }

    public async Task<IEnumerable<InvoiceDto>> SearchAsync(InvoiceSearchDto search)
    {
        var query = BuildSearchQuery(search);
        var invoices = await query
            .Include(i => i.Patient)
            .ToListAsync();
        return invoices.Select(EntityMapper.ToDto);
    }

    public async Task<(IEnumerable<InvoiceDto> Items, int TotalCount)> GetPagedAsync(InvoiceSearchDto search)
    {
        var query = BuildSearchQuery(search);
        var totalCount = await query.CountAsync();

        var invoices = await query
            .Include(i => i.Patient)
            .OrderByDescending(i => i.InvoiceDate)
            .Skip((search.Page - 1) * search.PageSize)
            .Take(search.PageSize)
            .ToListAsync();

        return (invoices.Select(EntityMapper.ToDto), totalCount);
    }

    public async Task<IEnumerable<InvoiceDto>> GetByPatientIdAsync(int patientId)
    {
        var invoices = await _dbSet
            .Include(i => i.Patient)
            .Where(i => i.PatientId == patientId)
            .OrderByDescending(i => i.InvoiceDate)
            .ToListAsync();
        return invoices.Select(EntityMapper.ToDto);
    }

    public async Task<IEnumerable<InvoiceDto>> GetOverdueInvoicesAsync()
    {
        var today = DateTime.UtcNow.Date;
        var invoices = await _dbSet
            .Include(i => i.Patient)
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
        var invoice = EntityMapper.ToEntity(dto);
        invoice.InvoiceNumber = await GenerateInvoiceNumberAsync();
        
        await _dbSet.AddAsync(invoice);
        await _context.SaveChangesAsync();
        return invoice.Id;
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

    private IQueryable<Invoice> BuildSearchQuery(InvoiceSearchDto search)
    {
        var query = _dbSet.AsQueryable();

        if (search.PatientId.HasValue)
            query = query.Where(i => i.PatientId == search.PatientId.Value);

        if (search.Status.HasValue)
            query = query.Where(i => i.Status == search.Status.Value);

        if (search.FromDate.HasValue)
            query = query.Where(i => i.InvoiceDate >= search.FromDate.Value);

        if (search.ToDate.HasValue)
            query = query.Where(i => i.InvoiceDate <= search.ToDate.Value);

        return query;
    }
}