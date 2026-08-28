using FluentAssertions;
using IntelliMed.Core.DTOs;
using IntelliMed.Core.Entities;
using IntelliMed.Infrastructure.Data;
using IntelliMed.Infrastructure.Repositories;
using IntelliMed.Tests.Helpers;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace IntelliMed.Tests.Repositories;

public class MedicineRepositoryTests : IDisposable
{
    private readonly MedicineRepository _repository;
    private readonly AppDbContext _context;

    public MedicineRepositoryTests()
    {
        _context = TestDbContextFactory.CreateInMemoryContext();
        _repository = new MedicineRepository(_context);
    }

    public void Dispose() => _context.Dispose();

    private async Task<int> AddMedicineAsync(
        string name,
        string? genericName = null,
        bool isActive = true,
        MedicineSource source = MedicineSource.Manual)
    {
        var medicine = new Medicine
        {
            Name = name,
            GenericName = genericName,
            IsActive = isActive,
            Source = source
        };
        _context.Medicines.Add(medicine);
        await _context.SaveChangesAsync();
        return medicine.Id;
    }

    [Fact]
    public async Task CreateAsync_LinksIngredientsFromSemicolonSeparatedNames()
    {
        var id = await _repository.CreateAsync(new CreateMedicineDto
        {
            Name = "Panadeine",
            ActiveIngredientNames = "Paracetamol;Codeine"
        });

        var created = await _context.Medicines
            .Include(m => m.Ingredients).ThenInclude(i => i.ActiveIngredient)
            .FirstAsync(m => m.Id == id);

        created.Ingredients.Select(i => i.ActiveIngredient!.Name)
            .Should().BeEquivalentTo("Paracetamol", "Codeine");
    }

    [Fact]
    public async Task CreateAsync_ReusesAnExistingIngredientRatherThanDuplicatingIt()
    {
        await _repository.CreateAsync(new CreateMedicineDto { Name = "Panamax", ActiveIngredientNames = "Paracetamol" });
        await _repository.CreateAsync(new CreateMedicineDto { Name = "Dymadon", ActiveIngredientNames = "Paracetamol" });

        var matching = await _context.ActiveIngredients.CountAsync(i => i.Name == "Paracetamol");
        matching.Should().Be(1);
    }

    [Fact]
    public async Task CreateAsync_MarksTheRowAsManuallyCreated()
    {
        var id = await _repository.CreateAsync(new CreateMedicineDto { Name = "Handtyped Drug" });

        var created = await _context.Medicines.FindAsync(id);
        created!.Source.Should().Be(MedicineSource.Manual);
        created.IsActive.Should().BeTrue();
    }

    [Fact]
    public async Task SearchAsync_MatchesOnNamePrefix()
    {
        await AddMedicineAsync("Amoxil");
        await AddMedicineAsync("Zoloft");

        var results = await _repository.SearchAsync("amox");

        results.Should().ContainSingle().Which.Name.Should().Be("Amoxil");
    }

    [Fact]
    public async Task SearchAsync_MatchesOnGenericNameSubstring()
    {
        await AddMedicineAsync("Brandname", genericName: "Amoxicillin");

        var results = await _repository.SearchAsync("oxicill");

        results.Should().ContainSingle().Which.Name.Should().Be("Brandname");
    }

    [Fact]
    public async Task SearchAsync_ExcludesInactiveMedicines()
    {
        await AddMedicineAsync("Retired Drug", isActive: false);

        var results = await _repository.SearchAsync("Retired");

        results.Should().BeEmpty();
    }

    [Fact]
    public async Task SearchAsync_CanFilterBySource()
    {
        await AddMedicineAsync("Zentry Manual", source: MedicineSource.Manual);
        await AddMedicineAsync("Zentry Synced", source: MedicineSource.Synced);

        var manualOnly = await _repository.SearchAsync("Zentry", MedicineSource.Manual);

        manualOnly.Should().ContainSingle().Which.Name.Should().Be("Zentry Manual");
    }

    [Fact]
    public async Task SearchAsync_MatchesNameByPrefixOnly_NotBySubstring()
    {
        // Documents actual behaviour: Name uses StartsWith while GenericName uses Contains, so a
        // mid-word fragment of a brand name deliberately does not match.
        await AddMedicineAsync("Amoxil Forte");

        var byPrefix = await _repository.SearchAsync("Amox");
        var byMidWord = await _repository.SearchAsync("Forte");

        byPrefix.Should().ContainSingle();
        byMidWord.Should().BeEmpty();
    }

    [Fact]
    public async Task SearchAsync_WithBlankQuery_ReturnsRecentUpToTakeLimit()
    {
        for (var i = 0; i < 5; i++) await AddMedicineAsync($"Drug {i}");

        var results = await _repository.SearchAsync("", take: 3);

        results.Should().HaveCount(3);
    }

    [Fact]
    public async Task UpdateAsync_RelinksIngredientsFromScratch()
    {
        var id = await _repository.CreateAsync(new CreateMedicineDto
        {
            Name = "Combo",
            ActiveIngredientNames = "Paracetamol;Codeine"
        });

        await _repository.UpdateAsync(id, new UpdateMedicineDto
        {
            Name = "Combo",
            ActiveIngredientNames = "Ibuprofen",
            IsActive = true
        });

        var updated = await _context.Medicines
            .Include(m => m.Ingredients).ThenInclude(i => i.ActiveIngredient)
            .FirstAsync(m => m.Id == id);

        updated.Ingredients.Select(i => i.ActiveIngredient!.Name)
            .Should().BeEquivalentTo(new[] { "Ibuprofen" }, "the previous links are replaced, not merged");
    }

    [Fact]
    public async Task UpdateAsync_WithUnknownId_IsANoOpRatherThanThrowing()
    {
        var act = async () => await _repository.UpdateAsync(9999, new UpdateMedicineDto { Name = "Ghost" });

        await act.Should().NotThrowAsync();
    }

    [Fact]
    public async Task DeactivateAsync_SoftDeletesRatherThanRemovingTheRow()
    {
        var id = await AddMedicineAsync("Discontinued");

        await _repository.DeactivateAsync(id);

        var stillThere = await _context.Medicines.FindAsync(id);
        stillThere.Should().NotBeNull("medicines are never hard-deleted — prescriptions FK to them");
        stillThere!.IsActive.Should().BeFalse();
    }

    [Fact]
    public async Task GetByIdAsync_ReturnsNullForUnknownId()
    {
        var result = await _repository.GetByIdAsync(9999);

        result.Should().BeNull();
    }

    [Fact]
    public async Task ExportCsvAsync_IncludesActiveMedicinesAndTheirIngredients()
    {
        await _repository.CreateAsync(new CreateMedicineDto
        {
            Name = "Exportable",
            ActiveIngredientNames = "Paracetamol"
        });

        var csv = await _repository.ExportCsvAsync();

        csv.Should().Contain("Exportable");
        csv.Should().Contain("Paracetamol");
    }

    [Fact]
    public async Task ExportCsvAsync_ExcludesInactiveMedicines()
    {
        await AddMedicineAsync("Hidden Drug", isActive: false);

        var csv = await _repository.ExportCsvAsync();

        csv.Should().NotContain("Hidden Drug");
    }

    [Fact]
    public async Task ExportCsvAsync_ToleratesAnIngredientLinkWithNoIngredientRow()
    {
        // Guards the null-check in ExportCsvAsync: MedicineIngredient.ActiveIngredient is a nullable
        // navigation, and one orphaned bridge row must not take down the whole export.
        var medicineId = await AddMedicineAsync("Orphan Link Drug");
        _context.MedicineIngredients.Add(new MedicineIngredient
        {
            MedicineId = medicineId,
            ActiveIngredientId = 424242 // deliberately points at nothing
        });
        await _context.SaveChangesAsync();

        var act = async () => await _repository.ExportCsvAsync();

        await act.Should().NotThrowAsync();
    }
}
