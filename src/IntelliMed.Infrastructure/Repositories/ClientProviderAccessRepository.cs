using IntelliMed.Core.DTOs;
using IntelliMed.Core.Entities;
using IntelliMed.Core.Interfaces;
using IntelliMed.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace IntelliMed.Infrastructure.Repositories;

public class ClientProviderAccessRepository : IClientProviderAccessRepository
{
    private readonly AppDbContext _context;

    public ClientProviderAccessRepository(AppDbContext context)
    {
        _context = context;
    }

    public async Task<List<ClientProviderAccessDto>> GetForClientAsync(int clientId)
    {
        var client = await _context.Clients.AsNoTracking()
            .Where(c => c.Id == clientId)
            .Select(c => new { c.CreatedByPractitionerId })
            .FirstOrDefaultAsync();

        var grants = await _context.ClientProviderAccesses.AsNoTracking()
            .Include(a => a.Practitioner)
            .Where(a => a.ClientId == clientId)
            .ToListAsync();

        return grants.Select(a => new ClientProviderAccessDto
        {
            PractitionerId = a.PractitionerId,
            PractitionerName = a.Practitioner == null ? string.Empty : $"{a.Practitioner.Title} {a.Practitioner.FirstName} {a.Practitioner.LastName}".Trim(),
            IsCreator = client != null && client.CreatedByPractitionerId == a.PractitionerId
        }).ToList();
    }

    public async Task SetForClientAsync(int clientId, IEnumerable<int> practitionerIds)
    {
        var client = await _context.Clients.FindAsync(clientId);
        if (client == null)
            throw new InvalidOperationException($"Client with ID {clientId} not found");

        var desired = practitionerIds.ToHashSet();
        // The creator's own grant can never be removed, even if the caller's submitted set omits it.
        if (client.CreatedByPractitionerId.HasValue)
            desired.Add(client.CreatedByPractitionerId.Value);

        var existing = await _context.ClientProviderAccesses
            .Where(a => a.ClientId == clientId)
            .ToListAsync();
        var existingIds = existing.Select(a => a.PractitionerId).ToHashSet();

        var toRemove = existing.Where(a => !desired.Contains(a.PractitionerId));
        var toAdd = desired
            .Where(id => !existingIds.Contains(id))
            .Select(id => new ClientProviderAccess { ClientId = clientId, PractitionerId = id });

        _context.ClientProviderAccesses.RemoveRange(toRemove);
        await _context.ClientProviderAccesses.AddRangeAsync(toAdd);
        await _context.SaveChangesAsync();
    }
}
