using FluentAssertions;
using IntelliMed.Core.DTOs;
using IntelliMed.Core.Entities;
using IntelliMed.Infrastructure.Data;
using IntelliMed.Infrastructure.Repositories;
using IntelliMed.Tests.Helpers;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace IntelliMed.Tests.Repositories;

public class PrescriptionRepositoryTests : IDisposable
{
    private readonly PrescriptionRepository _repository;
    private readonly AppDbContext _context;
    private readonly int _clientId;
    private readonly int _practitionerId;

    public PrescriptionRepositoryTests()
    {
        _context = TestDbContextFactory.CreateInMemoryContext();
        _repository = new PrescriptionRepository(_context);

        var client = new Client { FirstName = "Rx", LastName = "Test" };
        var practitioner = new Practitioner { FirstName = "Jane", LastName = "Doe", Title = "Dr" };
        _context.Clients.Add(client);
        _context.Practitioners.Add(practitioner);
        _context.SaveChanges();
        _clientId = client.Id;
        _practitionerId = practitioner.Id;
    }

    public void Dispose() => _context.Dispose();

    private int AddMedicine(string name, bool isPbsListed = true, PbsRestrictionLevel? restriction = PbsRestrictionLevel.AuthorityRequired)
    {
        var medicine = new Medicine { Name = name, IsPbsListed = isPbsListed, RestrictionLevel = restriction, Source = MedicineSource.Manual };
        _context.Medicines.Add(medicine);
        _context.SaveChanges();
        return medicine.Id;
    }

    private CreatePrescriptionDto BaseDto(int? medicineId = null, string medicineName = "Amoxil") => new()
    {
        ClientId = _clientId,
        MedicineId = medicineId,
        MedicineName = medicineName,
        Dose = "500mg",
        Frequency = "tds",
        Repeats = 2,
        PrescribedByPractitionerId = _practitionerId
    };

    [Fact]
    public async Task CreateAsync_WithCatalogMedicine_SnapshotsRestrictionFieldsAndGeneratesScriptNumber()
    {
        var medicineId = AddMedicine("Amoxil");

        var id = await _repository.CreateAsync(BaseDto(medicineId));

        var created = await _context.Prescriptions.FindAsync(id);
        created.Should().NotBeNull();
        created!.IsPbsListed.Should().BeTrue();
        created.RestrictionLevel.Should().Be(PbsRestrictionLevel.AuthorityRequired);
        created.ScriptNumber.Should().StartWith($"RX-{DateTime.UtcNow.Year}-");
        created.Status.Should().Be(PrescriptionStatus.Active);
    }

    [Fact]
    public async Task CreateAsync_WithFreeTextMedicine_LeavesSnapshotFieldsAtDefault()
    {
        var id = await _repository.CreateAsync(BaseDto(medicineId: null, medicineName: "Compounded Cream"));

        var created = await _context.Prescriptions.FindAsync(id);
        created!.MedicineId.Should().BeNull();
        created.IsPbsListed.Should().BeFalse();
        created.RestrictionLevel.Should().BeNull();
    }

    [Fact]
    public async Task CreateAsync_GeneratesSequentialScriptNumbersPerYear()
    {
        var id1 = await _repository.CreateAsync(BaseDto());
        var id2 = await _repository.CreateAsync(BaseDto());

        var first = await _context.Prescriptions.FindAsync(id1);
        var second = await _context.Prescriptions.FindAsync(id2);

        first!.ScriptNumber.Should().EndWith("00001");
        second!.ScriptNumber.Should().EndWith("00002");
    }

    [Fact]
    public async Task UpdateAsync_BeforeAnyIssue_UpdatesDoseAndFrequency()
    {
        var id = await _repository.CreateAsync(BaseDto());

        await _repository.UpdateAsync(id, new UpdatePrescriptionDto { Dose = "1g", Frequency = "bd", Repeats = 5 });

        var updated = await _context.Prescriptions.FindAsync(id);
        updated!.Dose.Should().Be("1g");
        updated.Frequency.Should().Be("bd");
        updated.Repeats.Should().Be(5);
    }

    [Fact]
    public async Task UpdateAsync_AfterIssue_IgnoresDoseButAppliesNotes()
    {
        var id = await _repository.CreateAsync(BaseDto());
        _context.PrescriptionIssues.Add(new PrescriptionIssue { PrescriptionId = id, IssuedByPractitionerId = _practitionerId, PrescriberName = "Dr Doe" });
        await _context.SaveChangesAsync();

        await _repository.UpdateAsync(id, new UpdatePrescriptionDto
        {
            Dose = "should not apply",
            Frequency = "should not apply",
            Repeats = 99,
            NotesForPharmacy = "please substitute generic"
        });

        var updated = await _context.Prescriptions.FindAsync(id);
        updated!.Dose.Should().Be("500mg"); // unchanged from BaseDto
        updated.Repeats.Should().Be(2); // unchanged
        updated.NotesForPharmacy.Should().Be("please substitute generic"); // notes always apply
    }

    [Fact]
    public async Task UpdateAsync_WithNonExistingPrescription_ThrowsException()
    {
        await Assert.ThrowsAsync<InvalidOperationException>(() => _repository.UpdateAsync(999, new UpdatePrescriptionDto()));
    }

    [Fact]
    public async Task CeaseAsync_SetsStatusAndCeasedDate()
    {
        var id = await _repository.CreateAsync(BaseDto());

        await _repository.CeaseAsync(id);

        var updated = await _context.Prescriptions.FindAsync(id);
        updated!.Status.Should().Be(PrescriptionStatus.Ceased);
        updated.CeasedDate.Should().NotBeNull();
    }

    [Fact]
    public async Task CancelAsync_SetsStatusCancelled()
    {
        var id = await _repository.CreateAsync(BaseDto());

        await _repository.CancelAsync(id);

        var updated = await _context.Prescriptions.FindAsync(id);
        updated!.Status.Should().Be(PrescriptionStatus.Cancelled);
    }

    [Fact]
    public async Task GetByClientIdAsync_DefaultsToActiveOnlyAndSetsIsIssued()
    {
        var activeId = await _repository.CreateAsync(BaseDto(medicineName: "Active One"));
        var ceasedId = await _repository.CreateAsync(BaseDto(medicineName: "Ceased One"));
        await _repository.CeaseAsync(ceasedId);
        _context.PrescriptionIssues.Add(new PrescriptionIssue { PrescriptionId = activeId, IssuedByPractitionerId = _practitionerId, PrescriberName = "Dr Doe" });
        await _context.SaveChangesAsync();

        var result = (await _repository.GetByClientIdAsync(_clientId)).ToList();

        result.Should().ContainSingle();
        result[0].MedicineName.Should().Be("Active One");
        result[0].IsIssued.Should().BeTrue();
    }

    [Fact]
    public async Task GetByClientIdAsync_IncludeInactiveReturnsAll()
    {
        await _repository.CreateAsync(BaseDto(medicineName: "Active One"));
        var ceasedId = await _repository.CreateAsync(BaseDto(medicineName: "Ceased One"));
        await _repository.CeaseAsync(ceasedId);

        var result = (await _repository.GetByClientIdAsync(_clientId, includeInactive: true)).ToList();

        result.Should().HaveCount(2);
    }

    [Fact]
    public async Task CreateAsync_PersistsWizardFields()
    {
        var episode = new Episode { ClientId = _clientId, Title = "Right knee injury" };
        _context.Episodes.Add(episode);
        await _context.SaveChangesAsync();

        var dto = BaseDto();
        dto.Instruction = "Take with food";
        dto.Purpose = PrescriptionSourceCode.PrintedPrescription;
        dto.IsLimitedDuration = true;
        dto.Reason = "Acute sinusitis";
        dto.EpisodeId = episode.Id;
        dto.StartDate = new DateTime(2026, 1, 1);

        var id = await _repository.CreateAsync(dto);

        var created = await _context.Prescriptions.FindAsync(id);
        created!.Instruction.Should().Be("Take with food");
        created.Purpose.Should().Be(PrescriptionSourceCode.PrintedPrescription);
        created.IsLimitedDuration.Should().BeTrue();
        created.Reason.Should().Be("Acute sinusitis");
        created.EpisodeId.Should().Be(episode.Id);
        created.StartDate.Should().Be(new DateTime(2026, 1, 1));
    }

    [Fact]
    public async Task GetByIdAsync_IncludesEpisodeTitle()
    {
        var episode = new Episode { ClientId = _clientId, Title = "Right knee injury" };
        _context.Episodes.Add(episode);
        await _context.SaveChangesAsync();

        var dto = BaseDto();
        dto.EpisodeId = episode.Id;
        var id = await _repository.CreateAsync(dto);

        var result = await _repository.GetByIdAsync(id);

        result!.EpisodeTitle.Should().Be("Right knee injury");
    }

    [Fact]
    public async Task UpdateAsync_AfterIssue_IgnoresClinicalFieldsButAppliesReasonAndEpisode()
    {
        var episode = new Episode { ClientId = _clientId, Title = "Right knee injury" };
        _context.Episodes.Add(episode);
        await _context.SaveChangesAsync();

        var id = await _repository.CreateAsync(BaseDto());
        _context.PrescriptionIssues.Add(new PrescriptionIssue { PrescriptionId = id, IssuedByPractitionerId = _practitionerId, PrescriberName = "Dr Doe" });
        await _context.SaveChangesAsync();

        await _repository.UpdateAsync(id, new UpdatePrescriptionDto
        {
            Instruction = "should not apply",
            Purpose = PrescriptionSourceCode.HandwrittenPrescription,
            IsLimitedDuration = true,
            Reason = "clarified reason",
            EpisodeId = episode.Id
        });

        var updated = await _context.Prescriptions.FindAsync(id);
        updated!.Instruction.Should().BeNull(); // clinical field, locked once issued
        updated.Purpose.Should().BeNull(); // clinical field, locked once issued
        updated.IsLimitedDuration.Should().BeFalse(); // clinical field, locked once issued
        updated.Reason.Should().Be("clarified reason"); // always editable
        updated.EpisodeId.Should().Be(episode.Id); // always editable
    }

    [Fact]
    public async Task GetActiveIngredientsForClientAsync_ExcludesCeasedAndFreeTextPrescriptions()
    {
        var ingredient = new ActiveIngredient { Name = "Amoxicillin" };
        _context.ActiveIngredients.Add(ingredient);
        await _context.SaveChangesAsync();

        var medicineId = AddMedicine("Amoxil");
        _context.MedicineIngredients.Add(new MedicineIngredient { MedicineId = medicineId, ActiveIngredientId = ingredient.Id });
        await _context.SaveChangesAsync();

        var activeId = await _repository.CreateAsync(BaseDto(medicineId, "Amoxil"));
        var ceasedId = await _repository.CreateAsync(BaseDto(medicineId, "Amoxil (ceased course)"));
        await _repository.CeaseAsync(ceasedId);
        await _repository.CreateAsync(BaseDto(medicineId: null, medicineName: "Compounded Cream")); // free-text, no ingredients to resolve

        var result = await _repository.GetActiveIngredientsForClientAsync(_clientId);

        result.Should().ContainSingle();
        result[0].IngredientId.Should().Be(ingredient.Id);
        result[0].MedicineName.Should().Be("Amoxil");
    }
}
