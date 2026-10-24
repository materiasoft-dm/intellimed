using System.Security.Claims;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using IntelliMed.Core.DTOs;
using IntelliMed.Core.Entities;
using IntelliMed.Core.Interfaces;
using IntelliMed.Api.Authorization;

namespace IntelliMed.Api.Controllers;

[ApiController]
[Route("api/clients")]
public class ClientsController : ControllerBase
{
    private readonly IClientRepository _clientRepository;
    private readonly IClientProviderAccessRepository _providerAccessRepository;
    private readonly IPractitionerRepository _practitionerRepository;
    private readonly UserManager<ApplicationUser> _userManager;

    public ClientsController(
        IClientRepository clientRepository,
        IClientProviderAccessRepository providerAccessRepository,
        IPractitionerRepository practitionerRepository,
        UserManager<ApplicationUser> userManager)
    {
        _clientRepository = clientRepository;
        _providerAccessRepository = providerAccessRepository;
        _practitionerRepository = practitionerRepository;
        _userManager = userManager;
    }

    /// <summary>
    /// Search clients with optional query and active filter, returning paged results.
    /// </summary>
    /// <param name="search">Search parameters (query, isActive, page, pageSize).</param>
    /// <returns>Paged list of clients and total count.</returns>
    [RequirePage(PageAccess.ClientLookup)]
    [HttpGet("search")]
    [ProducesResponseType(typeof(PagedResult<ClientDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> Search([FromQuery] ClientSearchDto search)
    {
        search.ClinicId = GetCurrentClinicId();
        search.RestrictToPractitionerId = await ResolveRestrictionAsync();
        var (items, totalCount) = await _clientRepository.GetPagedAsync(search);
        return Ok(new PagedResult<ClientDto>
        {
            Items = items,
            TotalCount = totalCount,
            Page = search.Page,
            PageSize = search.PageSize
        });
    }

    /// <summary>
    /// Create a new client. A caller linked to a Practitioner (see Practitioner.ApplicationUserId)
    /// is locked in as the creator, defaulting the record to visible only to them unless they opt
    /// into "visible to all". A caller with no linked Practitioner (Receptionist/Admin/unlinked
    /// Doctor — the common front-desk intake case) always gets visible-to-all, regardless of what
    /// was posted, since there's no "creator's own provider" to narrow it to.
    /// </summary>
    [RequirePage("clients/create")]
    [HttpPost]
    [ProducesResponseType(typeof(int), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Create([FromBody] CreateClientDto dto)
    {
        if (!ModelState.IsValid)
            return BadRequest(ModelState);

        dto.ClinicId = GetCurrentClinicId() ?? 1;

        var myPractitionerId = await ResolveMyPractitionerIdAsync();
        dto.CreatedByPractitionerId = myPractitionerId;
        if (!myPractitionerId.HasValue)
        {
            dto.VisibleToAllProviders = true;
        }

        var id = await _clientRepository.CreateAsync(dto);
        await _providerAccessRepository.SetForClientAsync(id, dto.ProviderIds);
        return CreatedAtAction(nameof(GetById), new { id }, new { id });
    }

    /// <summary>
    /// Get a single client by id. Returns 404 (not just for missing ids) when the caller is
    /// provider-restricted and isn't granted access to this client — same response either way, so
    /// this never confirms to a caller whether a given id merely exists.
    /// </summary>
    [RequirePage(PageAccess.ClientLookup)]
    [HttpGet("{id:int}")]
    [ProducesResponseType(typeof(ClientDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetById(int id)
    {
        var restriction = await ResolveRestrictionAsync();
        var client = await _clientRepository.GetByIdAsync(id, restriction);
        if (client == null) return NotFound();
        return Ok(client);
    }

    /// <summary>
    /// Update an existing client, including its provider-access list. The creator's own grant can
    /// never be removed via ProviderIds — see ClientProviderAccessRepository.SetForClientAsync.
    /// </summary>
    [RequirePage("clients/edit")]
    [HttpPut("{id:int}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Update(int id, [FromBody] UpdateClientDto dto)
    {
        if (!ModelState.IsValid)
            return BadRequest(ModelState);

        var restriction = await ResolveRestrictionAsync();
        var existing = await _clientRepository.GetByIdAsync(id, restriction);
        if (existing == null) return NotFound();

        await _clientRepository.UpdateAsync(id, dto);
        await _providerAccessRepository.SetForClientAsync(id, dto.ProviderIds);
        return NoContent();
    }

    /// <summary>
    /// Archive (soft-delete) a client by setting IsActive to false.
    /// </summary>
    [RequirePage("clients/delete")]
    [HttpDelete("{id:int}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Archive(int id)
    {
        var restriction = await ResolveRestrictionAsync();
        var existing = await _clientRepository.GetByIdAsync(id, restriction);
        if (existing == null) return NotFound();

        await _clientRepository.ArchiveAsync(id);
        return NoContent();
    }

    /// <summary>
    /// Reads the caller's currently selected clinic from the X-Clinic-Id header set by the Web client.
    /// Null/missing (e.g. existing tests that don't send it) means "no clinic filtering" for search,
    /// and defaults to clinic 1 for create.
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

    /// <summary>
    /// Resolves the per-provider visibility restriction for the current caller: null means
    /// unrestricted (SuperAdmin/Admin always, or any login with no linked Practitioner — matches
    /// today's behavior of everyone seeing every patient); a value limits results to clients visible
    /// to that practitioner. See ClientRepository.BuildSearchQuery/GetByIdAsync.
    /// </summary>
    private async Task<int?> ResolveRestrictionAsync()
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (userId == null) return null;

        var user = await _userManager.FindByIdAsync(userId);
        if (user == null) return null;

        var roles = await _userManager.GetRolesAsync(user);
        if (roles.Contains("SuperAdmin") || roles.Contains("Admin")) return null;

        return await ResolveMyPractitionerIdAsync(userId);
    }

    private async Task<int?> ResolveMyPractitionerIdAsync(string? userId = null)
    {
        userId ??= User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (userId == null) return null;

        var practitioner = await _practitionerRepository.GetByApplicationUserIdAsync(userId);
        return practitioner?.Id;
    }
}
