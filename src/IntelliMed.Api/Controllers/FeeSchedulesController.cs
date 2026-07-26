using Microsoft.AspNetCore.Mvc;
using IntelliMed.Core.DTOs;
using IntelliMed.Core.Interfaces;

namespace IntelliMed.Api.Controllers;

[ApiController]
[Route("api/fee-schedules")]
public class FeeSchedulesController : ControllerBase
{
    private readonly IFeeScheduleRepository _repository;
    private readonly IFeeScheduleImportService _importService;

    public FeeSchedulesController(IFeeScheduleRepository repository, IFeeScheduleImportService importService)
    {
        _repository = repository;
        _importService = importService;
    }

    /// <summary>
    /// Search fee schedules with optional code/description filters and archived toggle, returning paged results.
    /// </summary>
    [HttpGet("search")]
    [ProducesResponseType(typeof(PagedResult<FeeScheduleDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> Search([FromQuery] FeeScheduleSearchDto search)
    {
        var (items, totalCount) = await _repository.GetPagedAsync(search);
        return Ok(new PagedResult<FeeScheduleDto>
        {
            Items = items,
            TotalCount = totalCount,
            Page = search.Page,
            PageSize = search.PageSize
        });
    }

    /// <summary>
    /// Lightweight list of active fee schedules, used to populate the Health Fund/Fee Table dropdowns elsewhere.
    /// </summary>
    [HttpGet("active")]
    [ProducesResponseType(typeof(IEnumerable<FeeScheduleDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetAllActive()
    {
        var schedules = await _repository.GetAllActiveAsync();
        return Ok(schedules);
    }

    /// <summary>
    /// Create a new fee schedule.
    /// </summary>
    [HttpPost]
    [ProducesResponseType(typeof(int), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Create([FromBody] CreateFeeScheduleDto dto)
    {
        if (!ModelState.IsValid)
            return BadRequest(ModelState);

        var id = await _repository.CreateAsync(dto);
        return CreatedAtAction(nameof(GetById), new { id }, new { id });
    }

    /// <summary>
    /// Get a single fee schedule by id.
    /// </summary>
    [HttpGet("{id:int}")]
    [ProducesResponseType(typeof(FeeScheduleDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetById(int id)
    {
        var schedule = await _repository.GetByIdAsync(id);
        if (schedule == null) return NotFound();
        return Ok(schedule);
    }

    /// <summary>
    /// Update an existing fee schedule.
    /// </summary>
    [HttpPut("{id:int}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Update(int id, [FromBody] UpdateFeeScheduleDto dto)
    {
        if (!ModelState.IsValid)
            return BadRequest(ModelState);

        var existing = await _repository.GetByIdAsync(id);
        if (existing == null) return NotFound();

        await _repository.UpdateAsync(id, dto);
        return NoContent();
    }

    /// <summary>
    /// Archive (soft-delete) a fee schedule by setting IsArchived to true.
    /// </summary>
    [HttpPost("{id:int}/archive")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Archive(int id)
    {
        var existing = await _repository.GetByIdAsync(id);
        if (existing == null) return NotFound();

        await _repository.ArchiveAsync(id);
        return NoContent();
    }

    /// <summary>
    /// Permanently delete a fee schedule.
    /// </summary>
    [HttpDelete("{id:int}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Delete(int id)
    {
        var existing = await _repository.GetByIdAsync(id);
        if (existing == null) return NotFound();

        await _repository.DeleteAsync(id);
        return NoContent();
    }

    /// <summary>
    /// List the item-fee mappings for a fee schedule.
    /// </summary>
    [HttpGet("{id:int}/items")]
    [ProducesResponseType(typeof(IEnumerable<FeeScheduleItemDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetItems(int id)
    {
        var items = await _repository.GetItemsAsync(id);
        return Ok(items);
    }

    /// <summary>
    /// Add a single item-fee mapping to a fee schedule.
    /// </summary>
    [HttpPost("{id:int}/items")]
    [ProducesResponseType(typeof(int), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> AddItem(int id, [FromBody] CreateFeeScheduleItemDto dto)
    {
        if (!ModelState.IsValid)
            return BadRequest(ModelState);

        var itemId = await _repository.AddItemAsync(id, dto);
        return CreatedAtAction(nameof(GetItems), new { id }, new { id = itemId });
    }

    /// <summary>
    /// Update the fee of a single item-fee mapping.
    /// </summary>
    [HttpPut("items/{itemId:int}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> UpdateItemFee(int itemId, [FromBody] UpdateFeeScheduleItemFeeDto dto)
    {
        await _repository.UpdateItemFeeAsync(itemId, dto.Fee);
        return NoContent();
    }

    /// <summary>
    /// Every fee this item has held, most recent first — lets a user see when a price last moved.
    /// </summary>
    [HttpGet("items/{itemId:int}/history")]
    [ProducesResponseType(typeof(IEnumerable<FeeScheduleItemHistoryDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetItemHistory(int itemId)
    {
        var history = await _repository.GetItemHistoryAsync(itemId);
        return Ok(history);
    }

    /// <summary>
    /// Remove a single item-fee mapping.
    /// </summary>
    [HttpDelete("items/{itemId:int}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> RemoveItem(int itemId)
    {
        await _repository.RemoveItemAsync(itemId);
        return NoContent();
    }

    /// <summary>
    /// Reset every existing item's fee to the current MBS schedule fee.
    /// </summary>
    [HttpPost("{id:int}/items/copy-from-mbs")]
    [ProducesResponseType(typeof(int), StatusCodes.Status200OK)]
    public async Task<IActionResult> CopyFeesFromMbs(int id)
    {
        var updated = await _repository.CopyFeesFromMbsAsync(id);
        return Ok(updated);
    }

    /// <summary>
    /// Bulk-populate this schedule's items from another schedule's item-fee list (or the whole MBS
    /// catalog when SourceFeeScheduleId is null), optionally applying a percentage/flat adjustment.
    /// </summary>
    [HttpPost("{id:int}/items/import")]
    [ProducesResponseType(typeof(int), StatusCodes.Status200OK)]
    public async Task<IActionResult> ImportItems(int id, [FromBody] ImportFeeScheduleItemsRequest request)
    {
        var affected = await _repository.ImportItemsAsync(id, request);
        return Ok(affected);
    }

    /// <summary>
    /// Find-or-creates the BBGP/BBO/BBI bulk-bill schedules and (re)populates their items from the
    /// MBS catalog's 100% benefit, so bulk-bill rebate resolution works without any external file.
    /// </summary>
    [HttpPost("seed-bulk-bill")]
    [ProducesResponseType(typeof(SeedBulkBillResultDto), StatusCodes.Status200OK)]
    public async Task<IActionResult> SeedBulkBillSchedules()
    {
        var result = await _repository.SeedBulkBillSchedulesAsync();
        return Ok(result);
    }

    /// <summary>
    /// Find-or-creates the VAGP/VASO/VASI DVA schedule shells. No fee auto-population — populate via
    /// the generic CSV importer or manual Add Item once a DVA fee schedule CSV is available.
    /// </summary>
    [HttpPost("seed-dva")]
    [ProducesResponseType(typeof(SeedBulkBillResultDto), StatusCodes.Status200OK)]
    public async Task<IActionResult> SeedDvaScheduleShells()
    {
        var result = await _repository.SeedDvaScheduleShellsAsync();
        return Ok(result);
    }

    /// <summary>
    /// Downloads and parses the schedule's configured SourceUrl now, on demand (the same operation
    /// the background auto-import service runs periodically).
    /// </summary>
    [HttpPost("{id:int}/fetch-now")]
    [ProducesResponseType(typeof(FeeScheduleFetchResultDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> FetchNow(int id)
    {
        var existing = await _repository.GetByIdAsync(id);
        if (existing == null) return NotFound();

        var result = await _importService.FetchFromSourceAsync(id);
        return Ok(result);
    }
}
