using FluentAssertions;
using IntelliMed.Core.DTOs;
using IntelliMed.Core.Entities;
using IntelliMed.Infrastructure.Data;
using IntelliMed.Infrastructure.Repositories;
using IntelliMed.Tests.Helpers;
using Xunit;

namespace IntelliMed.Tests.Repositories;

public class ClientOccupationRepositoryTests : IDisposable
{
    private readonly ClientOccupationRepository _repository;
    private readonly AppDbContext _context;
    private readonly int _clientId;

    public ClientOccupationRepositoryTests()
    {
        _context = TestDbContextFactory.CreateInMemoryContext();
        _repository = new ClientOccupationRepository(_context);

        var client = new Client { FirstName = "Occ", LastName = "Test" };
        _context.Clients.Add(client);
        _context.SaveChanges();
        _clientId = client.Id;
    }

    public void Dispose() => _context.Dispose();

    [Fact]
    public async Task CreateAsync_WithValidDto_ReturnsNewOccupationId()
    {
        var dto = new CreateClientOccupationDto
        {
            ClientId = _clientId,
            Occupation = "Carpenter",
            HasAsbestos = true
        };

        var result = await _repository.CreateAsync(dto);

        result.Should().BeGreaterThan(0);
        var occupation = await _context.ClientOccupations.FindAsync(result);
        occupation!.Occupation.Should().Be("Carpenter");
        occupation.HasAsbestos.Should().BeTrue();
    }

    [Fact]
    public async Task ArchiveAsync_SetsIsArchivedTrue()
    {
        var occupation = new ClientOccupation { ClientId = _clientId, Occupation = "Miner" };
        _context.ClientOccupations.Add(occupation);
        await _context.SaveChangesAsync();

        await _repository.ArchiveAsync(occupation.Id);

        var updated = await _context.ClientOccupations.FindAsync(occupation.Id);
        updated!.IsArchived.Should().BeTrue();
    }
}
