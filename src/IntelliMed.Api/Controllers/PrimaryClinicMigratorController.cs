using IntelliMed.Core.DTOs;
using IntelliMed.Core.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Text.Json;

namespace IntelliMed.Api.Controllers;

/// <summary>
/// Imports legacy Pracnet patient and billing records into the Client/Invoice model. Called by the
/// IntelliMed.PrimaryClinicMigrator WinForms tool, which reads directly from an already-restored
/// legacy SQL Server database and POSTs each entity type here as JSON — this API never talks to
/// SQL Server itself. Restricted to SuperAdmin/Admin.
/// </summary>
[ApiController]
[Route("api/primary-clinic-migrator")]
[Authorize(Roles = "SuperAdmin,Admin")]
public class PrimaryClinicMigratorController : ControllerBase
{
    private readonly IPrimaryClinicMigratorService _migratorService;
    private readonly ILogger<PrimaryClinicMigratorController> _logger;

    public PrimaryClinicMigratorController(IPrimaryClinicMigratorService migratorService, ILogger<PrimaryClinicMigratorController> logger)
    {
        _migratorService = migratorService;
        _logger = logger;
    }

    // ── JSON-based per-entity endpoints (used by the WinForms PrimaryClinicMigrator tool) ──
    // An empty row set is a legitimate no-op, not an error — several legacy tables (e.g.
    // ContactAddresses, PreOccupations) are genuinely empty in real practice databases, and the
    // migrator tool runs every entity type unconditionally regardless of what a given install has.

    [HttpPost("import-clients")]
    [ProducesResponseType(typeof(PrimaryClinicMigratorResultDto), StatusCodes.Status200OK)]
    public async Task<ActionResult<PrimaryClinicMigratorResultDto>> ImportClients([FromBody] JsonImportRequest request)
    {
        if (request.Rows is not { Count: > 0 })
            return Ok(new PrimaryClinicMigratorResultDto());
        return Ok(await _migratorService.ImportClientsAsync(request.Rows, request.ClinicId));
    }

    [HttpPost("import-addresses")]
    [ProducesResponseType(typeof(PrimaryClinicMigratorResultDto), StatusCodes.Status200OK)]
    public async Task<ActionResult<PrimaryClinicMigratorResultDto>> ImportAddresses([FromBody] JsonImportRequest request)
    {
        if (request.Rows is not { Count: > 0 })
            return Ok(new PrimaryClinicMigratorResultDto());
        return Ok(await _migratorService.ImportAddressesAsync(request.Rows));
    }

    [HttpPost("import-referrals")]
    [ProducesResponseType(typeof(PrimaryClinicMigratorResultDto), StatusCodes.Status200OK)]
    public async Task<ActionResult<PrimaryClinicMigratorResultDto>> ImportReferrals([FromBody] JsonImportRequest request)
    {
        if (request.Rows is not { Count: > 0 })
            return Ok(new PrimaryClinicMigratorResultDto());
        return Ok(await _migratorService.ImportReferralsAsync(request.Rows));
    }

    [HttpPost("import-occupations")]
    [ProducesResponseType(typeof(PrimaryClinicMigratorResultDto), StatusCodes.Status200OK)]
    public async Task<ActionResult<PrimaryClinicMigratorResultDto>> ImportOccupations([FromBody] JsonImportRequest request)
    {
        if (request.Rows is not { Count: > 0 })
            return Ok(new PrimaryClinicMigratorResultDto());
        return Ok(await _migratorService.ImportOccupationsAsync(request.Rows));
    }

    [HttpPost("import-family-relationships")]
    [ProducesResponseType(typeof(PrimaryClinicMigratorResultDto), StatusCodes.Status200OK)]
    public async Task<ActionResult<PrimaryClinicMigratorResultDto>> ImportFamilyRelationships([FromBody] JsonImportRequest request)
    {
        if (request.Rows is not { Count: > 0 })
            return Ok(new PrimaryClinicMigratorResultDto());
        return Ok(await _migratorService.ImportFamilyRelationshipsAsync(request.Rows));
    }

    [HttpPost("import-compensation-claims")]
    [ProducesResponseType(typeof(PrimaryClinicMigratorResultDto), StatusCodes.Status200OK)]
    public async Task<ActionResult<PrimaryClinicMigratorResultDto>> ImportCompensationClaims([FromBody] JsonImportRequest request)
    {
        if (request.Rows is not { Count: > 0 })
            return Ok(new PrimaryClinicMigratorResultDto());
        return Ok(await _migratorService.ImportCompensationClaimsAsync(request.Rows));
    }

    [HttpPost("import-invoices")]
    [ProducesResponseType(typeof(PrimaryClinicMigratorResultDto), StatusCodes.Status200OK)]
    public async Task<ActionResult<PrimaryClinicMigratorResultDto>> ImportInvoices([FromBody] JsonImportRequest request)
    {
        if (request.Rows is not { Count: > 0 })
            return Ok(new PrimaryClinicMigratorResultDto());
        return Ok(await _migratorService.ImportInvoicesAsync(request.Rows));
    }

    [HttpPost("import-invoice-items")]
    [ProducesResponseType(typeof(PrimaryClinicMigratorResultDto), StatusCodes.Status200OK)]
    public async Task<ActionResult<PrimaryClinicMigratorResultDto>> ImportInvoiceItems([FromBody] JsonImportRequest request)
    {
        if (request.Rows is not { Count: > 0 })
            return Ok(new PrimaryClinicMigratorResultDto());
        return Ok(await _migratorService.ImportInvoiceItemsAsync(request.Rows));
    }

    [HttpPost("import-payments")]
    [ProducesResponseType(typeof(PrimaryClinicMigratorResultDto), StatusCodes.Status200OK)]
    public async Task<ActionResult<PrimaryClinicMigratorResultDto>> ImportPayments([FromBody] JsonPaymentsImportRequest request)
    {
        if (request.Allocations is not { Count: > 0 } && request.Payments is not { Count: > 0 })
            return Ok(new PrimaryClinicMigratorResultDto());
        return Ok(await _migratorService.ImportPaymentsAsync(request.Allocations, request.Payments));
    }
}

public class JsonImportRequest
{
    public int ClinicId { get; set; }
    public List<Dictionary<string, JsonElement>> Rows { get; set; } = new();
}

public class JsonPaymentsImportRequest
{
    public int ClinicId { get; set; }
    public List<Dictionary<string, JsonElement>> Allocations { get; set; } = new();
    public List<Dictionary<string, JsonElement>> Payments { get; set; } = new();
}
