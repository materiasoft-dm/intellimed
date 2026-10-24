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
    private readonly int _drSmithId;
    private readonly int _drJonesId;

    public ClientReferralRepositoryTests()
    {
        _context = TestDbContextFactory.CreateInMemoryContext();
        _repository = new ClientReferralRepository(_context);

        var client = new Client { FirstName = "Ref", LastName = "Test" };
        var drSmith = new AddressBookContact { Title = "Dr", FirstName = "Jane", LastName = "Smith", ProviderNumber = "2345678A" };
        var drJones = new AddressBookContact { Title = "Dr", LastName = "Jones" };
        _context.Clients.Add(client);
        _context.AddressBookContacts.AddRange(drSmith, drJones);
        _context.SaveChanges();
        _clientId = client.Id;
        _drSmithId = drSmith.Id;
        _drJonesId = drJones.Id;
    }

    public void Dispose() => _context.Dispose();

    [Fact]
    public async Task CreateAsync_WithValidDto_LinksTheAddressBookContact()
    {
        var dto = new CreateClientReferralDto
        {
            ClientId = _clientId,
            ReferralDate = new DateTime(2026, 1, 1),
            ReferringContactId = _drSmithId,
            IsGP = true
        };

        var result = await _repository.CreateAsync(dto);

        result.Should().BeGreaterThan(0);
        var referral = await _context.ClientReferrals.FindAsync(result);
        referral.Should().NotBeNull();
        referral!.ReferringContactId.Should().Be(_drSmithId);
        referral.IsGP.Should().BeTrue();
    }

    [Fact]
    public async Task GetByIdAsync_ReturnsTheContactsNameAndProviderNumber()
    {
        var referral = new ClientReferral { ClientId = _clientId, ReferralDate = DateTime.Today, ReferringContactId = _drSmithId };
        _context.ClientReferrals.Add(referral);
        await _context.SaveChangesAsync();
        _context.ChangeTracker.Clear();

        var dto = await _repository.GetByIdAsync(referral.Id);

        dto!.ReferringProviderName.Should().Be("Dr Jane Smith");
        dto.ReferringProviderNumber.Should().Be("2345678A");
    }

    [Fact]
    public async Task GetByClientIdAsync_ReturnsOnlyThatClientsReferrals()
    {
        var other = new Client { FirstName = "Other", LastName = "Client" };
        _context.Clients.Add(other);
        await _context.SaveChangesAsync();

        _context.ClientReferrals.AddRange(
            new ClientReferral { ClientId = _clientId, ReferralDate = DateTime.Today, ReferringContactId = _drSmithId },
            new ClientReferral { ClientId = _clientId, ReferralDate = DateTime.Today, ReferringContactId = _drJonesId },
            new ClientReferral { ClientId = other.Id, ReferralDate = DateTime.Today, ReferringContactId = _drSmithId });
        await _context.SaveChangesAsync();

        var result = (await _repository.GetByClientIdAsync(_clientId)).ToList();

        result.Should().HaveCount(2);
        result.Should().OnlyContain(r => r.ClientId == _clientId);
    }

    [Fact]
    public async Task ArchiveAsync_SetsIsArchivedTrue()
    {
        var referral = new ClientReferral { ClientId = _clientId, ReferralDate = DateTime.Today, ReferringContactId = _drSmithId };
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
    public async Task UpdateAsync_WithExistingReferral_RepointsTheContact()
    {
        var referral = new ClientReferral { ClientId = _clientId, ReferralDate = DateTime.Today, ReferringContactId = _drSmithId };
        _context.ClientReferrals.Add(referral);
        await _context.SaveChangesAsync();

        var update = new UpdateClientReferralDto
        {
            ReferralDate = referral.ReferralDate,
            ReferringContactId = _drJonesId,
            IsGP = true
        };

        await _repository.UpdateAsync(referral.Id, update);

        var updated = await _context.ClientReferrals.FindAsync(referral.Id);
        updated!.ReferringContactId.Should().Be(_drJonesId);
        updated.IsGP.Should().BeTrue();
    }
}
