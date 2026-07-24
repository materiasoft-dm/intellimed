using IntelliMed.Core.DTOs;
using IntelliMed.Core.Entities;
using IntelliMed.Core.Interfaces;
using IntelliMed.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace IntelliMed.Infrastructure.Repositories;

public class ClientFamilyRelationshipRepository : Repository<ClientFamilyRelationship>, IClientFamilyRelationshipRepository
{
    public ClientFamilyRelationshipRepository(AppDbContext context) : base(context)
    {
    }

    public async Task<IEnumerable<ClientFamilyRelationshipDto>> GetByClientIdAsync(int clientId)
    {
        var relationships = await _dbSet
            .Where(r => r.ClientId == clientId)
            .Include(r => r.RelativeClient)
            .ToListAsync();

        return relationships.Select(r => new ClientFamilyRelationshipDto
        {
            Id = r.Id,
            ClientId = r.ClientId,
            RelativeClientId = r.RelativeClientId,
            RelativeName = r.RelativeClient != null ? $"{r.RelativeClient.FirstName} {r.RelativeClient.LastName}" : string.Empty,
            RelativeAddress = r.RelativeClient != null
                ? $"{r.RelativeClient.Address}, {r.RelativeClient.Suburb} {r.RelativeClient.State} {r.RelativeClient.Postcode}"
                : string.Empty,
            RelationshipType = r.RelationshipType,
            CreatedAt = r.CreatedAt
        });
    }

    public async Task<int> CreateAsync(CreateClientFamilyRelationshipDto dto)
    {
        var relationship = new ClientFamilyRelationship
        {
            ClientId = dto.ClientId,
            RelativeClientId = dto.RelativeClientId,
            RelationshipType = dto.RelationshipType,
            CreatedAt = DateTime.UtcNow
        };
        await _dbSet.AddAsync(relationship);
        await _context.SaveChangesAsync();
        return relationship.Id;
    }
}
