using FluentAssertions;
using IntelliMed.Core.DTOs;
using IntelliMed.Core.Entities;
using IntelliMed.Infrastructure.Data;
using IntelliMed.Infrastructure.Repositories;
using IntelliMed.Tests.Helpers;
using Xunit;

namespace IntelliMed.Tests.Repositories;

public class ClientFamilyRelationshipRepositoryTests : IDisposable
{
    private readonly ClientFamilyRelationshipRepository _repository;
    private readonly AppDbContext _context;
    private readonly int _clientId;
    private readonly int _relativeId;

    public ClientFamilyRelationshipRepositoryTests()
    {
        _context = TestDbContextFactory.CreateInMemoryContext();
        _repository = new ClientFamilyRelationshipRepository(_context);

        var client = new Client { FirstName = "Head", LastName = "OfFamily", Address = "1 St", Suburb = "Town", State = "VIC", Postcode = "3000" };
        var relative = new Client { FirstName = "Child", LastName = "OfFamily" };
        _context.Clients.AddRange(client, relative);
        _context.SaveChanges();
        _clientId = client.Id;
        _relativeId = relative.Id;
    }

    public void Dispose() => _context.Dispose();

    [Fact]
    public async Task CreateAsync_WithValidDto_ReturnsNewRelationshipId()
    {
        var dto = new CreateClientFamilyRelationshipDto
        {
            ClientId = _clientId,
            RelativeClientId = _relativeId,
            RelationshipType = "Child"
        };

        var result = await _repository.CreateAsync(dto);

        result.Should().BeGreaterThan(0);
        var relationship = await _context.ClientFamilyRelationships.FindAsync(result);
        relationship!.RelativeClientId.Should().Be(_relativeId);
    }

    [Fact]
    public async Task GetByClientIdAsync_ReturnsRelativeNameAndAddress()
    {
        _context.ClientFamilyRelationships.Add(new ClientFamilyRelationship
        {
            ClientId = _clientId,
            RelativeClientId = _relativeId,
            RelationshipType = "Child"
        });
        await _context.SaveChangesAsync();

        var result = (await _repository.GetByClientIdAsync(_clientId)).ToList();

        result.Should().ContainSingle();
        result[0].RelativeName.Should().Be("Child OfFamily");
        result[0].RelationshipType.Should().Be("Child");
    }
}
