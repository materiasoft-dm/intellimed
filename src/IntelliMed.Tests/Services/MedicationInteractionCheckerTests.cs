using FluentAssertions;
using IntelliMed.Core.Entities;
using IntelliMed.Core.Interfaces;
using IntelliMed.Infrastructure.Data;
using IntelliMed.Infrastructure.Repositories;
using IntelliMed.Infrastructure.Services;
using IntelliMed.Tests.Helpers;
using Xunit;

namespace IntelliMed.Tests.Services;

public class MedicationInteractionCheckerTests : IDisposable
{
    private readonly MedicationInteractionChecker _checker;
    private readonly AppDbContext _context;
    private readonly int _clientId;

    public MedicationInteractionCheckerTests()
    {
        _context = TestDbContextFactory.CreateInMemoryContext();
        _checker = new MedicationInteractionChecker(_context, new DrugInteractionRepository(_context));

        var client = new Client { FirstName = "Safety", LastName = "Test" };
        _context.Clients.Add(client);
        _context.SaveChanges();
        _clientId = client.Id;
    }

    public void Dispose() => _context.Dispose();

    private int AddIngredient(string name)
    {
        var ingredient = new ActiveIngredient { Name = name };
        _context.ActiveIngredients.Add(ingredient);
        _context.SaveChanges();
        return ingredient.Id;
    }

    [Fact]
    public async Task CheckAsync_WithNoCandidateIngredients_SetsCanFullyCheckFalse()
    {
        var result = await _checker.CheckAsync(new MedicationSafetyCheckRequest(
            _clientId, "Compounded Cream", null, [], []));

        result.CanFullyCheck.Should().BeFalse();
        result.HasWarnings.Should().BeFalse();
    }

    [Fact]
    public async Task CheckAsync_WithEmptyInteractionTable_SetsInteractionDatabaseEmptyTrue()
    {
        var warfarinId = AddIngredient("Warfarin");

        var result = await _checker.CheckAsync(new MedicationSafetyCheckRequest(
            _clientId, "Warfarin", null, [warfarinId], []));

        result.InteractionDatabaseEmpty.Should().BeTrue();
    }

    [Fact]
    public async Task CheckAsync_WithInteractingPair_ReturnsWarningNamingTheConflictingMedicine()
    {
        var warfarinId = AddIngredient("Warfarin");
        var aspirinId = AddIngredient("Aspirin");
        var (idA, idB) = warfarinId < aspirinId ? (warfarinId, aspirinId) : (aspirinId, warfarinId);
        _context.DrugInteractions.Add(new DrugInteraction
        {
            IngredientAId = idA,
            IngredientBId = idB,
            Severity = InteractionSeverity.Major,
            Description = "Increased bleeding risk",
            IsActive = true
        });
        await _context.SaveChangesAsync();

        var result = await _checker.CheckAsync(new MedicationSafetyCheckRequest(
            _clientId, "Aspirin", null, [aspirinId],
            [new ExistingMedicationIngredient(warfarinId, "Coumadin")]));

        result.InteractionWarnings.Should().ContainSingle();
        result.InteractionWarnings[0].ConflictingMedicineName.Should().Be("Coumadin");
        result.InteractionWarnings[0].Severity.Should().Be(InteractionSeverity.Major);
        result.HasWarnings.Should().BeTrue();
    }

    [Fact]
    public async Task CheckAsync_WithCeasedMedicationExcludedByCaller_DoesNotFireWarning()
    {
        // The checker itself has no notion of "ceased" — it only sees whatever the caller passes as
        // existingMedicationIngredients. This proves that a caller who excludes a ceased medication's
        // ingredient from that list correctly gets no warning for it.
        var warfarinId = AddIngredient("Warfarin");
        var aspirinId = AddIngredient("Aspirin");
        var (idA, idB) = warfarinId < aspirinId ? (warfarinId, aspirinId) : (aspirinId, warfarinId);
        _context.DrugInteractions.Add(new DrugInteraction { IngredientAId = idA, IngredientBId = idB, Severity = InteractionSeverity.Major, IsActive = true });
        await _context.SaveChangesAsync();

        var result = await _checker.CheckAsync(new MedicationSafetyCheckRequest(
            _clientId, "Aspirin", null, [aspirinId], [])); // warfarin omitted, as if ceased

        result.InteractionWarnings.Should().BeEmpty();
    }

    [Fact]
    public async Task CheckAsync_WithConfirmedAllergyMatch_ReturnsConfirmedWarning()
    {
        var penicillinId = AddIngredient("Penicillin");
        _context.ClientAllergies.Add(new ClientAllergy
        {
            ClientId = _clientId,
            ActiveIngredientId = penicillinId,
            Substance = "Penicillin",
            Severity = AllergySeverity.Severe
        });
        await _context.SaveChangesAsync();

        var result = await _checker.CheckAsync(new MedicationSafetyCheckRequest(
            _clientId, "Amoxil", "Amoxicillin", [penicillinId], []));

        result.AllergyWarnings.Should().ContainSingle();
        result.AllergyWarnings[0].IsConfirmedMatch.Should().BeTrue();
    }

    [Fact]
    public async Task CheckAsync_WithFreeTextAllergyNoFkSet_ReturnsPossibleMatchByName()
    {
        _context.ClientAllergies.Add(new ClientAllergy
        {
            ClientId = _clientId,
            ActiveIngredientId = null, // recorded as free text only, no catalog link
            Substance = "Penicillin"
        });
        await _context.SaveChangesAsync();

        var unrelatedIngredientId = AddIngredient("Some Other Ingredient");

        var result = await _checker.CheckAsync(new MedicationSafetyCheckRequest(
            _clientId, "Penicillin V", null, [unrelatedIngredientId], []));

        result.AllergyWarnings.Should().ContainSingle();
        result.AllergyWarnings[0].IsConfirmedMatch.Should().BeFalse();
    }

    [Fact]
    public async Task CheckAsync_ExcludesArchivedAllergies()
    {
        var penicillinId = AddIngredient("Penicillin");
        _context.ClientAllergies.Add(new ClientAllergy
        {
            ClientId = _clientId,
            ActiveIngredientId = penicillinId,
            Substance = "Penicillin",
            IsArchived = true
        });
        await _context.SaveChangesAsync();

        var result = await _checker.CheckAsync(new MedicationSafetyCheckRequest(
            _clientId, "Amoxil", "Amoxicillin", [penicillinId], []));

        result.AllergyWarnings.Should().BeEmpty();
    }

    [Fact]
    public async Task CheckAsync_WithNoInteractionsOrAllergies_ReturnsCleanResult()
    {
        var ibuprofenId = AddIngredient("Ibuprofen");

        var result = await _checker.CheckAsync(new MedicationSafetyCheckRequest(
            _clientId, "Nurofen", "Ibuprofen", [ibuprofenId], []));

        result.HasWarnings.Should().BeFalse();
    }

    // ---- Duplicate therapy (PBS therapeutic groups) ------------------------

    private int AddMedicine(string name, string? therapeuticGroupId, string? groupTitle = null)
    {
        var medicine = new Medicine
        {
            Name = name,
            TherapeuticGroupId = therapeuticGroupId,
            TherapeuticGroupTitle = groupTitle
        };
        _context.Medicines.Add(medicine);
        _context.SaveChanges();
        return medicine.Id;
    }

    [Fact]
    public async Task CheckAsync_WhenExistingMedicineSharesTherapeuticGroup_WarnsDuplicateTherapy()
    {
        var ingredientId = AddIngredient("Atorvastatin");
        var candidateId = AddMedicine("Lipitor", "1150", "Statins");
        var existingId = AddMedicine("Crestor", "1150", "Statins");

        var result = await _checker.CheckAsync(new MedicationSafetyCheckRequest(
            _clientId, "Lipitor", "Atorvastatin", [ingredientId],
            [new ExistingMedicationIngredient(ingredientId, "Crestor", existingId)],
            candidateId));

        result.DuplicateTherapyWarnings.Should().ContainSingle();
        result.DuplicateTherapyWarnings[0].ExistingMedicineName.Should().Be("Crestor");
        result.DuplicateTherapyWarnings[0].TherapeuticGroupTitle.Should().Be("Statins");
        result.HasWarnings.Should().BeTrue();
    }

    [Fact]
    public async Task CheckAsync_WithDifferentTherapeuticGroups_DoesNotWarn()
    {
        var ingredientId = AddIngredient("Atorvastatin");
        var candidateId = AddMedicine("Lipitor", "1150");
        var existingId = AddMedicine("Panadol", "9999");

        var result = await _checker.CheckAsync(new MedicationSafetyCheckRequest(
            _clientId, "Lipitor", "Atorvastatin", [ingredientId],
            [new ExistingMedicationIngredient(ingredientId, "Panadol", existingId)],
            candidateId));

        result.DuplicateTherapyWarnings.Should().BeEmpty();
    }

    [Fact]
    public async Task CheckAsync_ReprescribingTheSameMedicine_IsNotADuplicate()
    {
        // A repeat of the same drug is a repeat, not duplicate therapy.
        var ingredientId = AddIngredient("Atorvastatin");
        var medicineId = AddMedicine("Lipitor", "1150", "Statins");

        var result = await _checker.CheckAsync(new MedicationSafetyCheckRequest(
            _clientId, "Lipitor", "Atorvastatin", [ingredientId],
            [new ExistingMedicationIngredient(ingredientId, "Lipitor", medicineId)],
            medicineId));

        result.DuplicateTherapyWarnings.Should().BeEmpty();
    }

    [Fact]
    public async Task CheckAsync_WhenCandidateHasNoTherapeuticGroup_DoesNotWarn()
    {
        // PBS only assigns a group where an equivalence exists, so a null group is common and must
        // not be treated as "matches everything else with a null group".
        var ingredientId = AddIngredient("Atorvastatin");
        var candidateId = AddMedicine("Ungrouped Drug", therapeuticGroupId: null);
        var existingId = AddMedicine("Another Ungrouped", therapeuticGroupId: null);

        var result = await _checker.CheckAsync(new MedicationSafetyCheckRequest(
            _clientId, "Ungrouped Drug", null, [ingredientId],
            [new ExistingMedicationIngredient(ingredientId, "Another Ungrouped", existingId)],
            candidateId));

        result.DuplicateTherapyWarnings.Should().BeEmpty();
    }

    [Fact]
    public async Task CheckAsync_WithFreeTextCandidate_SkipsDuplicateTherapy()
    {
        var ingredientId = AddIngredient("Atorvastatin");
        var existingId = AddMedicine("Crestor", "1150", "Statins");

        // No CandidateMedicineId — a compounded/free-text item has no catalogue row to group by.
        var result = await _checker.CheckAsync(new MedicationSafetyCheckRequest(
            _clientId, "Compounded mixture", null, [ingredientId],
            [new ExistingMedicationIngredient(ingredientId, "Crestor", existingId)]));

        result.DuplicateTherapyWarnings.Should().BeEmpty();
    }

    [Fact]
    public async Task CheckAsync_WhenExistingMedicineIsFreeText_IsSkippedNotCrashed()
    {
        // Free-text existing prescriptions carry no MedicineId.
        var ingredientId = AddIngredient("Atorvastatin");
        var candidateId = AddMedicine("Lipitor", "1150", "Statins");

        var result = await _checker.CheckAsync(new MedicationSafetyCheckRequest(
            _clientId, "Lipitor", "Atorvastatin", [ingredientId],
            [new ExistingMedicationIngredient(ingredientId, "Some compounded thing")],
            candidateId));

        result.DuplicateTherapyWarnings.Should().BeEmpty();
    }
}
