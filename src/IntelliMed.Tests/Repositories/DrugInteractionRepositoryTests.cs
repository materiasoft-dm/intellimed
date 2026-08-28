using FluentAssertions;
using IntelliMed.Core.DTOs;
using IntelliMed.Core.Entities;
using IntelliMed.Infrastructure.Data;
using IntelliMed.Infrastructure.Repositories;
using IntelliMed.Tests.Helpers;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace IntelliMed.Tests.Repositories;

public class DrugInteractionRepositoryTests : IDisposable
{
    private readonly DrugInteractionRepository _repository;
    private readonly AppDbContext _context;

    public DrugInteractionRepositoryTests()
    {
        _context = TestDbContextFactory.CreateInMemoryContext();
        _repository = new DrugInteractionRepository(_context);
    }

    public void Dispose() => _context.Dispose();

    [Fact]
    public async Task CreateAsync_WithNewIngredientNames_AutoCreatesIngredientsAndCreatesInteraction()
    {
        var dto = new CreateDrugInteractionDto
        {
            IngredientAName = "Warfarin",
            IngredientBName = "Aspirin",
            Severity = InteractionSeverity.Major,
            Description = "Increased bleeding risk"
        };

        var id = await _repository.CreateAsync(dto);

        var created = await _context.DrugInteractions.FindAsync(id);
        created.Should().NotBeNull();
        created!.Severity.Should().Be(InteractionSeverity.Major);
        (await _context.ActiveIngredients.CountAsync()).Should().Be(2);
    }

    [Fact]
    public async Task CreateAsync_CanonicallyOrdersIngredientsRegardlessOfInputOrder()
    {
        // Seed both ingredients up front with known ids so the canonical A<B outcome is deterministic.
        var warfarin = new ActiveIngredient { Name = "Warfarin" };
        var aspirin = new ActiveIngredient { Name = "Aspirin" };
        _context.ActiveIngredients.AddRange(warfarin, aspirin);
        await _context.SaveChangesAsync();

        var (lowerId, higherId) = warfarin.Id < aspirin.Id ? (warfarin.Id, aspirin.Id) : (aspirin.Id, warfarin.Id);
        var (lowerName, higherName) = warfarin.Id < aspirin.Id ? (warfarin.Name, aspirin.Name) : (aspirin.Name, warfarin.Name);

        var id = await _repository.CreateAsync(new CreateDrugInteractionDto
        {
            IngredientAName = higherName, // deliberately passed in reverse order
            IngredientBName = lowerName,
            Severity = InteractionSeverity.Moderate
        });

        var created = await _context.DrugInteractions.FindAsync(id);
        created!.IngredientAId.Should().Be(lowerId);
        created.IngredientBId.Should().Be(higherId);
    }

    [Fact]
    public async Task CountAsync_ReflectsTableSize()
    {
        (await _repository.CountAsync()).Should().Be(0);

        await _repository.CreateAsync(new CreateDrugInteractionDto { IngredientAName = "A", IngredientBName = "B", Severity = InteractionSeverity.Minor });

        (await _repository.CountAsync()).Should().Be(1);
    }

    [Fact]
    public async Task FindInteractionsAsync_ReturnsMatchRegardlessOfWhichStoredSideCandidateIsOn()
    {
        var a = new ActiveIngredient { Name = "IngredientA" };
        var b = new ActiveIngredient { Name = "IngredientB" };
        var unrelated = new ActiveIngredient { Name = "Unrelated" };
        _context.ActiveIngredients.AddRange(a, b, unrelated);
        await _context.SaveChangesAsync();

        var (idA, idB) = a.Id < b.Id ? (a.Id, b.Id) : (b.Id, a.Id);
        _context.DrugInteractions.Add(new DrugInteraction { IngredientAId = idA, IngredientBId = idB, Severity = InteractionSeverity.Major, IsActive = true });
        await _context.SaveChangesAsync();

        // Candidate is whichever ingredient ended up as the stored "B" side — the repository shouldn't
        // care which side the match lands on.
        var candidateId = idA == a.Id ? b.Id : a.Id;
        var existingId = candidateId == a.Id ? b.Id : a.Id;

        var matches = await _repository.FindInteractionsAsync(
            candidateIngredientIds: [candidateId],
            existingIngredientIds: [existingId]);

        matches.Should().HaveCount(1);

        var noMatches = await _repository.FindInteractionsAsync(
            candidateIngredientIds: [unrelated.Id],
            existingIngredientIds: [existingId]);
        noMatches.Should().BeEmpty();
    }

    [Fact]
    public async Task FindInteractionsAsync_IgnoresInactiveRows()
    {
        var a = new ActiveIngredient { Name = "IngredientA" };
        var b = new ActiveIngredient { Name = "IngredientB" };
        _context.ActiveIngredients.AddRange(a, b);
        await _context.SaveChangesAsync();

        _context.DrugInteractions.Add(new DrugInteraction { IngredientAId = a.Id, IngredientBId = b.Id, Severity = InteractionSeverity.Major, IsActive = false });
        await _context.SaveChangesAsync();

        var matches = await _repository.FindInteractionsAsync([a.Id], [b.Id]);

        matches.Should().BeEmpty();
    }
}
