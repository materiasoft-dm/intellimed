using Microsoft.AspNetCore.Authorization;
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
    private readonly IAccountTypeFeeScheduleMappingRepository _mappingRepository;

    public InvoicesController(
        IInvoiceRepository invoiceRepository,
        IAccountTypeFeeScheduleMappingRepository mappingRepository)
    {
        _invoiceRepository = invoiceRepository;
        _mappingRepository = mappingRepository;
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
    /// Resolve a line item's fee / rebate / GST for the current billing context (live UI preview).
    /// </summary>
    [HttpPost("resolve-line")]
    [ProducesResponseType(typeof(ResolveLineResult), StatusCodes.Status200OK)]
    public async Task<IActionResult> ResolveLine([FromBody] ResolveLineRequest request)
    {
        request.ClinicId = GetCurrentClinicId() ?? 1;
        var result = await _invoiceRepository.ResolveLineAsync(request);
        return Ok(result);
    }

    /// <summary>
    /// List the account-type → fee-schedule mappings for the current clinic (one row per account type).
    /// </summary>
    [HttpGet("fee-schedule-mappings")]
    [ProducesResponseType(typeof(IEnumerable<AccountTypeFeeScheduleMappingDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetFeeScheduleMappings()
    {
        var mappings = await _mappingRepository.GetMappingsAsync(GetCurrentClinicId() ?? 1);
        return Ok(mappings);
    }

    /// <summary>
    /// Upsert (or clear) a single account-type → fee-schedule mapping for the current clinic.
    /// </summary>
    [HttpPut("fee-schedule-mappings")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> SaveFeeScheduleMapping([FromBody] SaveAccountTypeFeeScheduleMappingDto dto)
    {
        await _mappingRepository.SaveMappingAsync(GetCurrentClinicId() ?? 1, dto);
        return NoContent();
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
    /// Update an invoice's Medicare/DVA/fund claim-lodgement status — manual for now, since no
    /// electronic claiming pipeline exists yet to drive it automatically.
    /// </summary>
    [HttpPut("{id:int}/claim-status")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> UpdateClaimStatus(int id, [FromBody] UpdateInvoiceClaimStatusRequest request)
    {
        var existing = await _invoiceRepository.GetByIdAsync(id);
        if (existing == null) return NotFound();

        await _invoiceRepository.UpdateClaimStatusAsync(id, request.ClaimStatus);
        return NoContent();
    }

    /// <summary>
    /// Forgive part or all of an invoice's outstanding balance — no money moves.
    /// </summary>
    [HttpPost("{id:int}/write-off")]
    [Authorize(Roles = "SuperAdmin,Admin")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> WriteOff(int id, [FromBody] CreateWriteOffDto dto)
    {
        try
        {
            await _invoiceRepository.WriteOffAsync(id, dto);
            return NoContent();
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(ex.Message);
        }
    }

    /// <summary>
    /// Cancel an unpaid invoice.
    /// </summary>
    [HttpPost("{id:int}/cancel")]
    [Authorize(Roles = "SuperAdmin,Admin")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Cancel(int id, [FromBody] CancelInvoiceDto dto)
    {
        try
        {
            await _invoiceRepository.CancelAsync(id, dto);
            return NoContent();
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(ex.Message);
        }
    }

    /// <summary>
    /// Split an unpaid invoice's line items across one or more new sibling invoices.
    /// </summary>
    [HttpPost("{id:int}/split")]
    [Authorize(Roles = "SuperAdmin,Admin")]
    [ProducesResponseType(typeof(SplitInvoiceResultDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Split(int id, [FromBody] SplitInvoiceDto dto)
    {
        try
        {
            var result = await _invoiceRepository.SplitAsync(id, dto);
            return Ok(result);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(ex.Message);
        }
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

public class UpdateInvoiceClaimStatusRequest
{
    public ClaimStatus ClaimStatus { get; set; }
}
