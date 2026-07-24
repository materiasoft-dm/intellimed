using FluentAssertions;
using IntelliMed.Core.DTOs;
using IntelliMed.Core.Entities;
using IntelliMed.Infrastructure.Data;
using IntelliMed.Infrastructure.Repositories;
using IntelliMed.Tests.Helpers;
using Xunit;

namespace IntelliMed.Tests.Repositories;

public class ClientCompensationClaimRepositoryTests : IDisposable
{
    private readonly ClientCompensationClaimRepository _repository;
    private readonly AppDbContext _context;
    private readonly int _clientId;

    public ClientCompensationClaimRepositoryTests()
    {
        _context = TestDbContextFactory.CreateInMemoryContext();
        _repository = new ClientCompensationClaimRepository(_context);

        var client = new Client { FirstName = "Claim", LastName = "Test" };
        _context.Clients.Add(client);
        _context.SaveChanges();
        _clientId = client.Id;
    }

    public void Dispose() => _context.Dispose();

    [Fact]
    public async Task CreateAsync_WithValidDto_ReturnsNewClaimId()
    {
        var dto = new CreateClientCompensationClaimDto { ClientId = _clientId, ClaimNum = "WC-001" };

        var result = await _repository.CreateAsync(dto);

        result.Should().BeGreaterThan(0);
        var claim = await _context.ClientCompensationClaims.FindAsync(result);
        claim!.ClaimNum.Should().Be("WC-001");
    }

    [Fact]
    public async Task ArchiveAsync_SetsIsArchivedTrue()
    {
        var claim = new ClientCompensationClaim { ClientId = _clientId, ClaimNum = "WC-002" };
        _context.ClientCompensationClaims.Add(claim);
        await _context.SaveChangesAsync();

        await _repository.ArchiveAsync(claim.Id);

        var updated = await _context.ClientCompensationClaims.FindAsync(claim.Id);
        updated!.IsArchived.Should().BeTrue();
    }

    [Fact]
    public async Task GetByClientIdAsync_ReturnsOnlyThatClientsClaims()
    {
        _context.ClientCompensationClaims.Add(new ClientCompensationClaim { ClientId = _clientId, ClaimNum = "WC-003" });
        await _context.SaveChangesAsync();

        var result = (await _repository.GetByClientIdAsync(_clientId)).ToList();

        result.Should().ContainSingle(c => c.ClaimNum == "WC-003");
    }
}
