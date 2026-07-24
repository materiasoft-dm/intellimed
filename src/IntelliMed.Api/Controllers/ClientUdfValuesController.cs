using Microsoft.AspNetCore.Mvc;
using IntelliMed.Core.DTOs;
using IntelliMed.Core.Interfaces;

namespace IntelliMed.Api.Controllers;

[ApiController]
[Route("api/clients/{clientId:int}/udf-values")]
public class ClientUdfValuesController : ControllerBase
{
    private readonly IClientUserDefinedFieldValueRepository _repository;

    public ClientUdfValuesController(IClientUserDefinedFieldValueRepository repository)
    {
        _repository = repository;
    }

    [HttpGet]
    [ProducesResponseType(typeof(IEnumerable<ClientUserDefinedFieldValueDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetByClient(int clientId)
    {
        var values = await _repository.GetByClientIdAsync(clientId);
        return Ok(values);
    }

    [HttpPost]
    [ProducesResponseType(typeof(int), StatusCodes.Status201Created)]
    public async Task<IActionResult> Create(int clientId, [FromBody] CreateClientUserDefinedFieldValueDto dto)
    {
        dto.ClientId = clientId;
        var id = await _repository.CreateAsync(dto);
        return CreatedAtAction(nameof(GetByClient), new { clientId }, new { id });
    }

    [HttpPut("{id:int}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Update(int clientId, int id, [FromBody] UpdateClientUserDefinedFieldValueDto dto)
    {
        var existing = await _repository.GetByIdAsync(id);
        if (existing == null || existing.ClientId != clientId) return NotFound();

        await _repository.UpdateAsync(id, dto);
        return NoContent();
    }

    [HttpDelete("{id:int}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Delete(int clientId, int id)
    {
        var existing = await _repository.GetByIdAsync(id);
        if (existing == null || existing.ClientId != clientId) return NotFound();

        await _repository.DeleteAsync(id);
        return NoContent();
    }
}
