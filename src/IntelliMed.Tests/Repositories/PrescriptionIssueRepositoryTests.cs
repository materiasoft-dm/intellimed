using FluentAssertions;
using IntelliMed.Core.DTOs;
using IntelliMed.Core.Entities;
using IntelliMed.Infrastructure.Data;
using IntelliMed.Infrastructure.Repositories;
using IntelliMed.Tests.Helpers;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace IntelliMed.Tests.Repositories;

public class PrescriptionIssueRepositoryTests : IDisposable
{
    private readonly PrescriptionIssueRepository _repository;
    private readonly PrescriptionRepository _prescriptionRepository;
    private readonly AppDbContext _context;
    private readonly int _practitionerId;
    private int _prescriptionId;

    public PrescriptionIssueRepositoryTests()
    {
        _context = TestDbContextFactory.CreateInMemoryContext();
        _repository = new PrescriptionIssueRepository(_context);
        _prescriptionRepository = new PrescriptionRepository(_context);

        var client = new Client { FirstName = "Rx", LastName = "Test" };
        var practitioner = new Practitioner
        {
            FirstName = "Jane", LastName = "Doe", Title = "Dr",
            ProviderNumber = "12345A", AhpraNumber = "MED0001234567",
            ApplicationUserId = "linked-user"
        };
        _context.Clients.Add(client);
        _context.Practitioners.Add(practitioner);
        _context.Users.Add(new ApplicationUser
        {
            Id = "linked-user", Email = "jane.doe@clinic.com", UserName = "jane.doe@clinic.com",
            HpiiNumber = "8003614900012345", Qualifications = "MBBS FRACGP"
        });
        _context.SaveChanges();
        _practitionerId = practitioner.Id;
    }

    public void Dispose() => _context.Dispose();

    private async Task<int> CreatePrescriptionAsync(string? notesForPharmacy = null)
    {
        var id = await _prescriptionRepository.CreateAsync(new CreatePrescriptionDto
        {
            ClientId = _context.Clients.First().Id,
            MedicineName = "Amoxil",
            Dose = "500mg",
            Frequency = "tds",
            Repeats = 2,
            PrescribedByPractitionerId = _practitionerId,
            NotesForPharmacy = notesForPharmacy
        });
        _prescriptionId = id;
        return id;
    }

    [Fact]
    public async Task IssueAsync_CreatesSnapshotWithPrescriberDetailsFromPractitionerAndApplicationUser()
    {
        var prescriptionId = await CreatePrescriptionAsync();

        var issueId = await _repository.IssueAsync(prescriptionId, new IssuePrescriptionDto { IssuedByPractitionerId = _practitionerId });

        var issue = await _context.PrescriptionIssues.FindAsync(issueId);
        issue.Should().NotBeNull();
        issue!.PrescriberName.Should().Be("Dr Jane Doe");
        issue.PrescriberProviderNumber.Should().Be("12345A");
        issue.PrescriberAhpraNumber.Should().Be("MED0001234567");
        issue.PrescriberHpiiNumber.Should().Be("8003614900012345");
        issue.PrescriberQualifications.Should().Be("MBBS FRACGP");
        issue.Dose.Should().Be("500mg");
        issue.RepeatNumber.Should().Be(0);
    }

    [Fact]
    public async Task IssueAsync_WithNoLinkedApplicationUser_LeavesHpiiAndQualificationsNull()
    {
        var unlinkedPractitioner = new Practitioner { FirstName = "No", LastName = "Login", ApplicationUserId = null };
        _context.Practitioners.Add(unlinkedPractitioner);
        await _context.SaveChangesAsync();
        var prescriptionId = await CreatePrescriptionAsync();

        var issueId = await _repository.IssueAsync(prescriptionId, new IssuePrescriptionDto { IssuedByPractitionerId = unlinkedPractitioner.Id });

        var issue = await _context.PrescriptionIssues.FindAsync(issueId);
        issue!.PrescriberHpiiNumber.Should().BeNull();
        issue.PrescriberQualifications.Should().BeNull();
        issue.PrescriberName.Should().Be("No Login"); // Title is empty; Trim() drops the resulting leading space
    }

    [Fact]
    public async Task IssueAsync_IncrementsRepeatNumberOnSubsequentIssues()
    {
        var prescriptionId = await CreatePrescriptionAsync();

        var firstId = await _repository.IssueAsync(prescriptionId, new IssuePrescriptionDto { IssuedByPractitionerId = _practitionerId });
        var secondId = await _repository.IssueAsync(prescriptionId, new IssuePrescriptionDto { IssuedByPractitionerId = _practitionerId });

        (await _context.PrescriptionIssues.FindAsync(firstId))!.RepeatNumber.Should().Be(0);
        (await _context.PrescriptionIssues.FindAsync(secondId))!.RepeatNumber.Should().Be(1);
    }

    [Fact]
    public async Task IssueAsync_WithCeasedPrescription_ThrowsException()
    {
        // Found live: the Prescriptions tab was offering "Issue & Print" for a ceased-but-never-issued
        // prescription, since its UI condition only checked IsIssued, not Status. Fixed in the UI and
        // backstopped here — a ceased/cancelled order should never become issuable again.
        var prescriptionId = await CreatePrescriptionAsync();
        await _prescriptionRepository.CeaseAsync(prescriptionId);

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            _repository.IssueAsync(prescriptionId, new IssuePrescriptionDto { IssuedByPractitionerId = _practitionerId }));
    }

    [Fact]
    public async Task IssueAsync_SetsTransmissionStatusNotApplicableForPaperAndPendingForElectronic()
    {
        var prescriptionId = await CreatePrescriptionAsync();

        var paperId = await _repository.IssueAsync(prescriptionId,
            new IssuePrescriptionDto { IssuedByPractitionerId = _practitionerId, DeliveryMethod = PrescriptionDeliveryMethod.Paper });
        var electronicId = await _repository.IssueAsync(prescriptionId,
            new IssuePrescriptionDto { IssuedByPractitionerId = _practitionerId, DeliveryMethod = PrescriptionDeliveryMethod.Electronic });

        (await _context.PrescriptionIssues.FindAsync(paperId))!.TransmissionStatus.Should().Be(PrescriptionTransmissionStatus.NotApplicable);
        (await _context.PrescriptionIssues.FindAsync(electronicId))!.TransmissionStatus.Should().Be(PrescriptionTransmissionStatus.Pending);
    }

    [Fact]
    public async Task IssueAsync_SnapshotIsIndependentOfLaterPrescriptionNoteEdits()
    {
        // The whole point of the snapshot design: PrescribingNotes/NotesForPharmacy stay editable on
        // Prescription even after an issue exists, but each already-created PrescriptionIssue must
        // keep showing what was actually printed at the time, not today's edited value.
        var prescriptionId = await CreatePrescriptionAsync(notesForPharmacy: "Original pharmacy note");

        var firstIssueId = await _repository.IssueAsync(prescriptionId, new IssuePrescriptionDto { IssuedByPractitionerId = _practitionerId });

        await _prescriptionRepository.UpdateAsync(prescriptionId, new UpdatePrescriptionDto
        {
            NotesForPharmacy = "Updated pharmacy note",
            Repeats = 2 // Repeats/Dose/etc are locked post-issue so re-send unchanged values
        });

        var secondIssueId = await _repository.IssueAsync(prescriptionId, new IssuePrescriptionDto { IssuedByPractitionerId = _practitionerId });

        var firstIssue = await _repository.GetByIdAsync(firstIssueId);
        var secondIssue = await _repository.GetByIdAsync(secondIssueId);

        firstIssue!.NotesForPharmacy.Should().Be("Original pharmacy note");
        secondIssue!.NotesForPharmacy.Should().Be("Updated pharmacy note");
    }

    [Fact]
    public async Task IssueAsync_WithNonExistingPrescription_ThrowsException()
    {
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            _repository.IssueAsync(999, new IssuePrescriptionDto { IssuedByPractitionerId = _practitionerId }));
    }

    [Fact]
    public async Task GetByIdAsync_ResolvesClientAndScriptNumberFromParentPrescription()
    {
        var prescriptionId = await CreatePrescriptionAsync();
        var issueId = await _repository.IssueAsync(prescriptionId, new IssuePrescriptionDto { IssuedByPractitionerId = _practitionerId });

        var dto = await _repository.GetByIdAsync(issueId);

        dto!.MedicineName.Should().Be("Amoxil");
        dto.ClientName.Should().Be("Rx Test");
        dto.ScriptNumber.Should().NotBeNullOrEmpty();
    }
}
