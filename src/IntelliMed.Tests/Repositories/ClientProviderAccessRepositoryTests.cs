using FluentAssertions;
using IntelliMed.Core.Entities;
using IntelliMed.Infrastructure.Data;
using IntelliMed.Infrastructure.Repositories;
using IntelliMed.Tests.Helpers;
using Xunit;

namespace IntelliMed.Tests.Repositories;

public class ClientProviderAccessRepositoryTests : IDisposable
{
    private readonly ClientProviderAccessRepository _repository;
    private readonly AppDbContext _context;

    public ClientProviderAccessRepositoryTests()
    {
        _context = TestDbContextFactory.CreateInMemoryContext();
        _repository = new ClientProviderAccessRepository(_context);
    }

    public void Dispose()
    {
        _context.Dispose();
    }

    private async Task<Practitioner> CreatePractitionerAsync(string lastName)
    {
        var practitioner = new Practitioner { FirstName = "Dr", LastName = lastName, Title = "Dr", Profession = "GP", Email = $"{lastName.ToLower()}@example.com", Phone = "0400000000" };
        _context.Practitioners.Add(practitioner);
        await _context.SaveChangesAsync();
        return practitioner;
    }

    private async Task<Client> CreateClientAsync(int? createdByPractitionerId)
    {
        var client = new Client { FirstName = "Test", LastName = "Patient", IsActive = true, VisibleToAllProviders = false, CreatedByPractitionerId = createdByPractitionerId };
        _context.Clients.Add(client);
        await _context.SaveChangesAsync();
        return client;
    }

    [Fact]
    public async Task SetForClientAsync_GrantsListedPractitioners()
    {
        // Arrange
        var creator = await CreatePractitionerAsync("Creator");
        var other = await CreatePractitionerAsync("Other");
        var client = await CreateClientAsync(creator.Id);

        // Act
        await _repository.SetForClientAsync(client.Id, new[] { other.Id });

        // Assert
        var grants = await _repository.GetForClientAsync(client.Id);
        grants.Should().Contain(g => g.PractitionerId == other.Id && !g.IsCreator);
    }

    [Fact]
    public async Task SetForClientAsync_AlwaysIncludesCreator_EvenIfOmittedFromSubmittedSet()
    {
        // Arrange
        var creator = await CreatePractitionerAsync("Creator");
        var other = await CreatePractitionerAsync("Other");
        var client = await CreateClientAsync(creator.Id);
        await _repository.SetForClientAsync(client.Id, new[] { creator.Id, other.Id });

        // Act — a buggy or malicious caller submits a set that omits the creator entirely
        await _repository.SetForClientAsync(client.Id, new[] { other.Id });

        // Assert — the creator's grant survives regardless
        var grants = await _repository.GetForClientAsync(client.Id);
        grants.Should().Contain(g => g.PractitionerId == creator.Id && g.IsCreator);
    }

    [Fact]
    public async Task SetForClientAsync_RemovesGrantsNotInTheNewSet()
    {
        // Arrange
        var creator = await CreatePractitionerAsync("Creator");
        var other1 = await CreatePractitionerAsync("Other1");
        var other2 = await CreatePractitionerAsync("Other2");
        var client = await CreateClientAsync(creator.Id);
        await _repository.SetForClientAsync(client.Id, new[] { other1.Id, other2.Id });

        // Act
        await _repository.SetForClientAsync(client.Id, new[] { other1.Id });

        // Assert
        var grants = await _repository.GetForClientAsync(client.Id);
        grants.Should().Contain(g => g.PractitionerId == other1.Id);
        grants.Should().NotContain(g => g.PractitionerId == other2.Id);
    }

    [Fact]
    public async Task GetForClientAsync_WithNoGrants_ReturnsEmpty()
    {
        // Arrange
        var creator = await CreatePractitionerAsync("Creator");
        var client = await CreateClientAsync(creator.Id);

        // Act
        var grants = await _repository.GetForClientAsync(client.Id);

        // Assert — the creator lives on Client.CreatedByPractitionerId, not in the grants table
        // itself, until SetForClientAsync has been called at least once.
        grants.Should().BeEmpty();
    }
}
