using Microsoft.AspNetCore.Mvc;
using IntelliMed.Core.DTOs;
using IntelliMed.Core.Interfaces;

namespace IntelliMed.Api.Controllers;

[ApiController]
[Route("api/receipts")]
public class ReceiptsController : ControllerBase
{
    private readonly IReceiptRepository _receiptRepository;

    public ReceiptsController(IReceiptRepository receiptRepository)
    {
        _receiptRepository = receiptRepository;
    }

    /// <summary>
    /// Get a single receipt with its tenders and allocations.
    /// </summary>
    [HttpGet("{id:int}")]
    [ProducesResponseType(typeof(ReceiptDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetById(int id)
    {
        var receipt = await _receiptRepository.GetByIdAsync(id);
        if (receipt == null) return NotFound();
        return Ok(receipt);
    }

    /// <summary>
    /// Record a receipt: one or more tenders allocated across one or more invoices/line items for
    /// the same payer.
    /// </summary>
    [HttpPost]
    [ProducesResponseType(typeof(int), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Create([FromBody] CreateReceiptDto dto)
    {
        if (!ModelState.IsValid)
            return BadRequest(ModelState);

        dto.ClinicId = GetCurrentClinicId() ?? 1;

        try
        {
            var id = await _receiptRepository.CreateAsync(dto);
            return CreatedAtAction(nameof(GetById), new { id }, new { id });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(ex.Message);
        }
    }

    /// <summary>
    /// List a payer's outstanding (unpaid/partially-paid) invoices, for the multi-invoice receipt picker.
    /// </summary>
    [HttpGet("outstanding")]
    [ProducesResponseType(typeof(List<OutstandingInvoiceDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetOutstanding([FromQuery] int payerClientId, [FromQuery] int? excludeInvoiceId)
    {
        var result = await _receiptRepository.GetOutstandingInvoicesForPayerAsync(GetCurrentClinicId() ?? 1, payerClientId, excludeInvoiceId);
        return Ok(result);
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
