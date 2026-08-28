using FluentAssertions;
using IntelliMed.Core.DTOs;
using IntelliMed.Core.Entities;
using IntelliMed.Infrastructure.Data;
using IntelliMed.Infrastructure.Repositories;
using IntelliMed.Tests.Helpers;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace IntelliMed.Tests.Repositories;

public class ProgressNoteRepositoryTests : IDisposable
{
    private readonly ProgressNoteRepository _repository;
    private readonly AppDbContext _context;
    private readonly int _clientId;
    private readonly int _practitionerId;

    public ProgressNoteRepositoryTests()
    {
        _context = TestDbContextFactory.CreateInMemoryContext();
        _repository = new ProgressNoteRepository(_context);

        var client = new Client { FirstName = "Note", LastName = "Test" };
        var practitioner = new Practitioner { FirstName = "Jane", LastName = "Doe", Title = "Dr" };
        _context.Clients.Add(client);
        _context.Practitioners.Add(practitioner);
        _context.SaveChanges();
        _clientId = client.Id;
        _practitionerId = practitioner.Id;
    }

    public void Dispose() => _context.Dispose();

    private int AddAppointment(DateTime? appointmentDate = null)
    {
        var appointment = new Appointment
        {
            ClientId = _clientId,
            PractitionerId = _practitionerId,
            AppointmentDate = appointmentDate ?? DateTime.Today,
            StartTime = TimeSpan.FromHours(9),
            EndTime = TimeSpan.FromHours(9.5),
            Status = AppointmentStatus.Arrived,
            Type = AppointmentType.Standard
        };
        _context.Appointments.Add(appointment);
        _context.SaveChanges();
        return appointment.Id;
    }

    private CreateProgressNoteDto BaseDto(int? appointmentId = null, DateTime? encounterDate = null, string reasonForVisit = "Headache") => new()
    {
        ClientId = _clientId,
        AppointmentId = appointmentId,
        PractitionerId = _practitionerId,
        EncounterDate = encounterDate ?? DateTime.Today,
        ReasonForVisit = reasonForVisit,
        Subjective = "Reports frontal headache for 2 days",
        Objective = "Afebrile, obs normal",
        Assessment = "Tension headache",
        Plan = "Paracetamol, review if persists"
    };

    [Fact]
    public async Task CreateAsync_WithLinkedAppointment_DerivesEncounterDateFromAppointment()
    {
        var appointmentId = AddAppointment(appointmentDate: new DateTime(2026, 3, 1));

        var id = await _repository.CreateAsync(BaseDto(appointmentId, encounterDate: DateTime.Today));

        var created = await _context.ProgressNotes.FindAsync(id);
        created!.EncounterDate.Should().Be(new DateTime(2026, 3, 1));
    }

    [Fact]
    public async Task CreateAsync_WithoutAppointment_UsesSuppliedEncounterDate()
    {
        var suppliedDate = new DateTime(2026, 5, 5);

        var id = await _repository.CreateAsync(BaseDto(encounterDate: suppliedDate));

        var created = await _context.ProgressNotes.FindAsync(id);
        created!.EncounterDate.Should().Be(suppliedDate);
        created.Status.Should().Be(ProgressNoteStatus.Draft);
    }

    [Fact]
    public async Task CreateAsync_WithDeceasedClient_ThrowsException()
    {
        var deceasedClient = new Client { FirstName = "Deceased", LastName = "Client", Deceased = true };
        _context.Clients.Add(deceasedClient);
        await _context.SaveChangesAsync();

        var deceasedDto = new CreateProgressNoteDto
        {
            ClientId = deceasedClient.Id,
            PractitionerId = _practitionerId,
            EncounterDate = DateTime.Today,
            ReasonForVisit = "Should be rejected"
        };

        await Assert.ThrowsAsync<InvalidOperationException>(() => _repository.CreateAsync(deceasedDto));
    }

    [Fact]
    public async Task UpdateAsync_WhileDraft_UpdatesSoapFields()
    {
        var id = await _repository.CreateAsync(BaseDto());

        await _repository.UpdateAsync(id, new UpdateProgressNoteDto
        {
            PractitionerId = _practitionerId,
            EncounterDate = DateTime.Today,
            Assessment = "Migraine, not tension headache",
            Plan = "Sumatriptan"
        });

        var updated = await _context.ProgressNotes.FindAsync(id);
        updated!.Assessment.Should().Be("Migraine, not tension headache");
        updated.Plan.Should().Be("Sumatriptan");
    }

    [Fact]
    public async Task UpdateAsync_AfterFinalize_ThrowsException()
    {
        var id = await _repository.CreateAsync(BaseDto());
        await _repository.FinalizeAsync(id, new FinalizeProgressNoteDto { MarkAppointmentCompleted = false });

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            _repository.UpdateAsync(id, new UpdateProgressNoteDto { PractitionerId = _practitionerId, EncounterDate = DateTime.Today, Plan = "should not apply" }));
    }

    [Fact]
    public async Task FinalizeAsync_SetsStatusAndFinalizedAt()
    {
        var id = await _repository.CreateAsync(BaseDto());

        await _repository.FinalizeAsync(id, new FinalizeProgressNoteDto { MarkAppointmentCompleted = false });

        var finalized = await _context.ProgressNotes.FindAsync(id);
        finalized!.Status.Should().Be(ProgressNoteStatus.Finalized);
        finalized.FinalizedAt.Should().NotBeNull();
    }

    [Fact]
    public async Task FinalizeAsync_WithMarkAppointmentCompleted_CompletesLinkedAppointment()
    {
        var appointmentId = AddAppointment();
        var id = await _repository.CreateAsync(BaseDto(appointmentId));

        await _repository.FinalizeAsync(id, new FinalizeProgressNoteDto { MarkAppointmentCompleted = true });

        var appointment = await _context.Appointments.FindAsync(appointmentId);
        appointment!.Status.Should().Be(AppointmentStatus.Completed);
        appointment.CompletedAt.Should().NotBeNull();
    }

    [Fact]
    public async Task FinalizeAsync_AlreadyFinalized_ThrowsException()
    {
        var id = await _repository.CreateAsync(BaseDto());
        await _repository.FinalizeAsync(id, new FinalizeProgressNoteDto { MarkAppointmentCompleted = false });

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            _repository.FinalizeAsync(id, new FinalizeProgressNoteDto { MarkAppointmentCompleted = false }));
    }

    [Fact]
    public async Task DeleteAsync_WhileDraft_Succeeds()
    {
        var id = await _repository.CreateAsync(BaseDto());

        await _repository.DeleteAsync(id);

        (await _context.ProgressNotes.FindAsync(id)).Should().BeNull();
    }

    [Fact]
    public async Task DeleteAsync_AfterFinalize_ThrowsException()
    {
        var id = await _repository.CreateAsync(BaseDto());
        await _repository.FinalizeAsync(id, new FinalizeProgressNoteDto { MarkAppointmentCompleted = false });

        await Assert.ThrowsAsync<InvalidOperationException>(() => _repository.DeleteAsync(id));

        (await _context.ProgressNotes.FindAsync(id)).Should().NotBeNull(); // still there, not deleted
    }

    [Fact]
    public async Task AddAddendumAsync_WhileStillDraft_ThrowsException()
    {
        var id = await _repository.CreateAsync(BaseDto());

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            _repository.AddAddendumAsync(id, new AddProgressNoteAddendumDto { Content = "too early", CreatedByPractitionerId = _practitionerId }));
    }

    [Fact]
    public async Task AddAddendumAsync_AfterFinalize_AppendsPractitionerSourcedAddendum()
    {
        var id = await _repository.CreateAsync(BaseDto());
        await _repository.FinalizeAsync(id, new FinalizeProgressNoteDto { MarkAppointmentCompleted = false });

        await _repository.AddAddendumAsync(id, new AddProgressNoteAddendumDto { Content = "Patient called back, symptoms resolved", CreatedByPractitionerId = _practitionerId });

        var dto = await _repository.GetByIdAsync(id);
        dto!.Addenda.Should().ContainSingle();
        dto.Addenda[0].Content.Should().Be("Patient called back, symptoms resolved");
        dto.Addenda[0].Source.Should().Be(ProgressNoteAddendumSource.Practitioner);
    }

    [Fact]
    public async Task AppendSystemEntryAsync_WithOpenNoteForAppointment_AppendsSystemSourcedAddendum()
    {
        var appointmentId = AddAppointment();
        var id = await _repository.CreateAsync(BaseDto(appointmentId));

        await _repository.AppendSystemEntryAsync(appointmentId, "Prescribed: Panadol 500mg 1 tab bd", "Prescription", 42, _practitionerId);

        var dto = await _repository.GetByIdAsync(id);
        dto!.Addenda.Should().ContainSingle();
        dto.Addenda[0].Content.Should().Be("Prescribed: Panadol 500mg 1 tab bd");
        dto.Addenda[0].Source.Should().Be(ProgressNoteAddendumSource.System);
        dto.Addenda[0].SourceReferenceType.Should().Be("Prescription");
        dto.Addenda[0].SourceReferenceId.Should().Be(42);
    }

    [Fact]
    public async Task AppendSystemEntryAsync_WithNoNoteOpenForAppointment_NoOps()
    {
        var appointmentId = AddAppointment(); // no ProgressNote created for this appointment

        var act = async () => await _repository.AppendSystemEntryAsync(appointmentId, "Prescribed: Panadol", "Prescription", 42, _practitionerId);

        await act.Should().NotThrowAsync();
        (await _context.ProgressNoteAddenda.CountAsync()).Should().Be(0);
    }

    [Fact]
    public async Task AppendSystemEntryAsync_CanAppendToAnAlreadyFinalizedNote()
    {
        var appointmentId = AddAppointment();
        var id = await _repository.CreateAsync(BaseDto(appointmentId));
        await _repository.FinalizeAsync(id, new FinalizeProgressNoteDto { MarkAppointmentCompleted = false });

        await _repository.AppendSystemEntryAsync(appointmentId, "Prescribed: Panadol", "Prescription", 42, _practitionerId);

        var dto = await _repository.GetByIdAsync(id);
        dto!.Addenda.Should().ContainSingle();
    }

    [Fact]
    public async Task GetByClientIdAsync_OrdersNewestEncounterFirst()
    {
        await _repository.CreateAsync(BaseDto(encounterDate: new DateTime(2026, 1, 1), reasonForVisit: "Older"));
        await _repository.CreateAsync(BaseDto(encounterDate: new DateTime(2026, 6, 1), reasonForVisit: "Newer"));

        var result = (await _repository.GetByClientIdAsync(_clientId)).ToList();

        result.Should().HaveCount(2);
        result[0].ReasonForVisit.Should().Be("Newer");
    }
}
