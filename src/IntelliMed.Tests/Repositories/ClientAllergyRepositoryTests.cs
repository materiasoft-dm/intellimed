using FluentAssertions;
using IntelliMed.Core.DTOs;
using IntelliMed.Core.Entities;
using IntelliMed.Infrastructure.Data;
using IntelliMed.Infrastructure.Repositories;
using IntelliMed.Tests.Helpers;
using Xunit;

namespace IntelliMed.Tests.Repositories;

public class ClientAllergyRepositoryTests : IDisposable
{
    private readonly ClientAllergyRepository _repository;
    private readonly AppDbContext _context;
    private readonly int _clientId;

    public ClientAllergyRepositoryTests()
    {
        _context = TestDbContextFactory.CreateInMemoryContext();
        _repository = new ClientAllergyRepository(_context);

        var client = new Client { FirstName = "Allergy", LastName = "Test" };
        _context.Clients.Add(client);
        _context.SaveChanges();
        _clientId = client.Id;
    }

    public void Dispose() => _context.Dispose();

    [Fact]
    public async Task CreateAsync_WithValidDto_ReturnsNewAllergyId()
    {
        var dto = new CreateClientAllergyDto
        {
            ClientId = _clientId,
            Substance = "Penicillin",
            Type = AllergyType.Medication,
            Severity = AllergySeverity.Severe
        };

        var result = await _repository.CreateAsync(dto);

        result.Should().BeGreaterThan(0);
        var allergy = await _context.ClientAllergies.FindAsync(result);
        allergy.Should().NotBeNull();
        allergy!.Substance.Should().Be("Penicillin");
        allergy.Severity.Should().Be(AllergySeverity.Severe);
    }

    [Fact]
    public async Task GetByClientIdAsync_ReturnsOnlyThatClientsAllergiesAndResolvesIngredientName()
    {
        var ingredient = new ActiveIngredient { Name = "Amoxicillin" };
        _context.ActiveIngredients.Add(ingredient);
        await _context.SaveChangesAsync();

        var other = new Client { FirstName = "Other", LastName = "Client" };
        _context.Clients.Add(other);
        await _context.SaveChangesAsync();

        _context.ClientAllergies.AddRange(
            new ClientAllergy { ClientId = _clientId, Substance = "Amoxicillin", ActiveIngredientId = ingredient.Id },
            new ClientAllergy { ClientId = _clientId, Substance = "Latex", Type = AllergyType.Other },
            new ClientAllergy { ClientId = other.Id, Substance = "Peanuts", Type = AllergyType.Food });
        await _context.SaveChangesAsync();

        var result = (await _repository.GetByClientIdAsync(_clientId)).ToList();

        result.Should().HaveCount(2);
        result.Should().OnlyContain(a => a.ClientId == _clientId);
        result.Single(a => a.Substance == "Amoxicillin").ActiveIngredientName.Should().Be("Amoxicillin");
    }

    [Fact]
    public async Task ArchiveAsync_SetsIsArchivedTrue()
    {
        var allergy = new ClientAllergy { ClientId = _clientId, Substance = "Sulfa" };
        _context.ClientAllergies.Add(allergy);
        await _context.SaveChangesAsync();

        await _repository.ArchiveAsync(allergy.Id);

        var updated = await _context.ClientAllergies.FindAsync(allergy.Id);
        updated!.IsArchived.Should().BeTrue();
    }

    [Fact]
    public async Task ArchiveAsync_WithNonExistingAllergy_ThrowsException()
    {
        await Assert.ThrowsAsync<InvalidOperationException>(() => _repository.ArchiveAsync(999));
    }

    [Fact]
    public async Task UpdateAsync_WithExistingAllergy_UpdatesFields()
    {
        var allergy = new ClientAllergy { ClientId = _clientId, Substance = "Old", Severity = AllergySeverity.Mild };
        _context.ClientAllergies.Add(allergy);
        await _context.SaveChangesAsync();

        var update = new UpdateClientAllergyDto { Substance = "New", Type = AllergyType.Medication, Severity = AllergySeverity.Severe };

        await _repository.UpdateAsync(allergy.Id, update);

        var updated = await _context.ClientAllergies.FindAsync(allergy.Id);
        updated!.Substance.Should().Be("New");
        updated.Severity.Should().Be(AllergySeverity.Severe);
    }
}
