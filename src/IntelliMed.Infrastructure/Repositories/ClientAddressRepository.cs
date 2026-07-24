using IntelliMed.Core.DTOs;
using IntelliMed.Core.Entities;
using IntelliMed.Core.Interfaces;
using IntelliMed.Infrastructure.Data;
using IntelliMed.Infrastructure.Mappers;
using Microsoft.EntityFrameworkCore;

namespace IntelliMed.Infrastructure.Repositories;

public class ClientAddressRepository : Repository<ClientAddress>, IClientAddressRepository
{
    public ClientAddressRepository(AppDbContext context) : base(context)
    {
    }

    public async Task<ClientAddressDto?> GetByIdAsync(int id)
    {
        var address = await _dbSet.FindAsync(id);
        return address == null ? null : EntityMapper.ToDto(address);
    }

    public async Task<IEnumerable<ClientAddressDto>> GetByClientIdAsync(int clientId)
    {
        var addresses = await _dbSet
            .Where(a => a.ClientId == clientId)
            .ToListAsync();
        return addresses.Select(EntityMapper.ToDto);
    }

    public async Task<int> CreateAsync(CreateClientAddressDto dto)
    {
        var address = EntityMapper.ToEntity(dto);
        await _dbSet.AddAsync(address);
        await _context.SaveChangesAsync();
        return address.Id;
    }

    public async Task UpdateAsync(int id, UpdateClientAddressDto dto)
    {
        var address = await _dbSet.FindAsync(id);
        if (address == null)
            throw new InvalidOperationException($"ClientAddress with ID {id} not found");

        EntityMapper.UpdateEntity(address, dto);
        await _context.SaveChangesAsync();
    }
}
