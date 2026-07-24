using FluentAssertions;
using IntelliMed.Core.DTOs;
using IntelliMed.Core.Entities;
using IntelliMed.Infrastructure.Data;
using IntelliMed.Infrastructure.Repositories;
using IntelliMed.Tests.Helpers;
using Xunit;

namespace IntelliMed.Tests.Repositories;

public class ClientReferralRepositoryTests : IDisposable
{
    private readonly ClientReferralRepository _repository;
    private readonly AppDbContext _context;
    private readonly int _clientId;

    public ClientReferralRepositoryTests()
    {
        _context = TestDbContextFactory.CreateInMemoryContext();
        _repository = new ClientReferralRepository(_context);

        var client = new Client { FirstName = "Ref", LastName = "Test" };
        _context.Clients.Add(client);
        _context.SaveChanges();
        _clientId = client.Id;
    }

    public void Dispose() => _context.Dispose();

    [Fact]
    public async Task CreateAsync_WithValidDto_ReturnsNewReferralId()
    {
        var dto = new CreateClientReferralDto
        {
            ClientId = _clientId,
            ReferralDate = new DateTime(2026, 1, 1),
            ReferringProviderName = "Dr Smith",
            IsGP = true
        };

        var result = await _repository.CreateAsync(dto);

        result.Should().BeGreaterThan(0);
        var referral = await _context.ClientReferrals.FindAsync(result);
        referral.Should().NotBeNull();
        referral!.ReferringProviderName.Should().Be("Dr Smith");
        referral.IsGP.Should().BeTrue();
    }

    [Fact]
    public async Task GetByClientIdAsync_ReturnsOnlyThatClientsReferrals()
    {
        var other = new Client { FirstName = "Other", LastName = "Client" };
        _context.Clients.Add(other);
        await _context.SaveChangesAsync();

        _context.ClientReferrals.AddRange(
            new ClientReferral { ClientId = _clientId, ReferralDate = DateTime.Today, ReferringProviderName = "A" },
            new ClientReferral { ClientId = _clientId, ReferralDate = DateTime.Today, ReferringProviderName = "B" },
            new ClientReferral { ClientId = other.Id, ReferralDate = DateTime.Today, ReferringProviderName = "C" });
        await _context.SaveChangesAsync();

        var result = (await _repository.GetByClientIdAsync(_clientId)).ToList();

        result.Should().HaveCount(2);
        result.Should().OnlyContain(r => r.ClientId == _clientId);
    }

    [Fact]
    public async Task ArchiveAsync_SetsIsArchivedTrue()
    {
        var referral = new ClientReferral { ClientId = _clientId, ReferralDate = DateTime.Today, ReferringProviderName = "A" };
        _context.ClientReferrals.Add(referral);
        await _context.SaveChangesAsync();

        await _repository.ArchiveAsync(referral.Id);

        var updated = await _context.ClientReferrals.FindAsync(referral.Id);
        updated!.IsArchived.Should().BeTrue();
    }

    [Fact]
    public async Task ArchiveAsync_WithNonExistingReferral_ThrowsException()
    {
        await Assert.ThrowsAsync<InvalidOperationException>(() => _repository.ArchiveAsync(999));
    }

    [Fact]
    public async Task UpdateAsync_WithExistingReferral_UpdatesFields()
    {
        var referral = new ClientReferral { ClientId = _clientId, ReferralDate = DateTime.Today, ReferringProviderName = "Old" };
        _context.ClientReferrals.Add(referral);
        await _context.SaveChangesAsync();

        var update = new UpdateClientReferralDto
        {
            ReferralDate = referral.ReferralDate,
            ReferringProviderName = "New",
            IsGP = true
        };

        await _repository.UpdateAsync(referral.Id, update);

        var updated = await _context.ClientReferrals.FindAsync(referral.Id);
        updated!.ReferringProviderName.Should().Be("New");
        updated.IsGP.Should().BeTrue();
    }
}
