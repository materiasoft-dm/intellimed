using Microsoft.AspNetCore.Mvc;
using IntelliMed.Core.DTOs;
using IntelliMed.Core.Entities;
using IntelliMed.Core.Interfaces;

namespace IntelliMed.Api.Controllers;

[ApiController]
[Route("api/invoices")]
public class InvoicesController : ControllerBase
{
    private readonly IInvoiceRepository _invoiceRepository;

    public InvoicesController(IInvoiceRepository invoiceRepository)
    {
        _invoiceRepository = invoiceRepository;
    }

    /// <summary>
    /// Search invoices with optional client/status/date filters, returning paged results.
    /// </summary>
    [HttpGet("search")]
    [ProducesResponseType(typeof(PagedResult<InvoiceDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> Search([FromQuery] InvoiceSearchDto search)
    {
        search.ClinicId = GetCurrentClinicId();
        var (items, totalCount) = await _invoiceRepository.GetPagedAsync(search);
        return Ok(new PagedResult<InvoiceDto>
        {
            Items = items,
            TotalCount = totalCount,
            Page = search.Page,
            PageSize = search.PageSize
        });
    }

    /// <summary>
    /// Get all payments across invoices (for the flat "Payments" list page), with date/method filters.
    /// </summary>
    [HttpGet("payments")]
    [ProducesResponseType(typeof(PagedResult<PaymentDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetAllPayments([FromQuery] PaymentSearchDto search)
    {
        search.ClinicId = GetCurrentClinicId();
        var (items, totalCount) = await _invoiceRepository.GetAllPaymentsAsync(search);
        return Ok(new PagedResult<PaymentDto>
        {
            Items = items,
            TotalCount = totalCount,
            Page = search.Page,
            PageSize = search.PageSize
        });
    }

    /// <summary>
    /// Create a new invoice with its line items.
    /// </summary>
    [HttpPost]
    [ProducesResponseType(typeof(int), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Create([FromBody] CreateInvoiceDto dto)
    {
        if (!ModelState.IsValid)
            return BadRequest(ModelState);

        dto.ClinicId = GetCurrentClinicId() ?? 1;
        var id = await _invoiceRepository.CreateAsync(dto);
        return CreatedAtAction(nameof(GetById), new { id }, new { id });
    }

    /// <summary>
    /// Get a single invoice with its client, items, and payments.
    /// </summary>
    [HttpGet("{id:int}")]
    [ProducesResponseType(typeof(InvoiceDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetById(int id)
    {
        var invoice = await _invoiceRepository.GetByIdWithDetailsAsync(id);
        if (invoice == null) return NotFound();
        return Ok(invoice);
    }

    /// <summary>
    /// Record a payment against an invoice, updating its paid amount and status.
    /// </summary>
    [HttpPost("{id:int}/payments")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> AddPayment(int id, [FromBody] CreatePaymentDto dto)
    {
        if (!ModelState.IsValid)
            return BadRequest(ModelState);

        var existing = await _invoiceRepository.GetByIdAsync(id);
        if (existing == null) return NotFound();

        dto.InvoiceId = id;
        await _invoiceRepository.AddPaymentAsync(id, dto);
        return NoContent();
    }

    /// <summary>
    /// Update an invoice's status (e.g. Sent, Cancelled).
    /// </summary>
    [HttpPut("{id:int}/status")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> UpdateStatus(int id, [FromBody] UpdateInvoiceStatusRequest request)
    {
        var existing = await _invoiceRepository.GetByIdAsync(id);
        if (existing == null) return NotFound();

        await _invoiceRepository.UpdateStatusAsync(id, request.Status);
        return NoContent();
    }

    /// <summary>
    /// Reads the caller's currently selected clinic from the X-Clinic-Id header set by the Web client.
    /// </summary>
    private int? GetCurrentClinicId()
    {
        if (Request.Headers.TryGetValue("X-Clinic-Id", out var value) &&
            int.TryParse(value.ToString(), out var clinicId))
        {
            return clinicId;
        }
        return null;
    }
}

public class UpdateInvoiceStatusRequest
{
    public InvoiceStatus Status { get; set; }
}
