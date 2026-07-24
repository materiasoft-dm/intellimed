using Microsoft.AspNetCore.Mvc;
using IntelliMed.Core.DTOs;
using IntelliMed.Core.Interfaces;

namespace IntelliMed.Api.Controllers;

[ApiController]
[Route("api/clients/{clientId:int}/family")]
public class ClientFamilyController : ControllerBase
{
    private readonly IClientFamilyRelationshipRepository _repository;

    public ClientFamilyController(IClientFamilyRelationshipRepository repository)
    {
        _repository = repository;
    }

    [HttpGet]
    [ProducesResponseType(typeof(IEnumerable<ClientFamilyRelationshipDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetByClient(int clientId)
    {
        var relationships = await _repository.GetByClientIdAsync(clientId);
        return Ok(relationships);
    }

    [HttpPost]
    [ProducesResponseType(typeof(int), StatusCodes.Status201Created)]
    public async Task<IActionResult> Create(int clientId, [FromBody] CreateClientFamilyRelationshipDto dto)
    {
        dto.ClientId = clientId;
        var id = await _repository.CreateAsync(dto);
        return CreatedAtAction(nameof(GetByClient), new { clientId }, new { id });
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
