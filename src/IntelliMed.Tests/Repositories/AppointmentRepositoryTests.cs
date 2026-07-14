using FluentAssertions;
using IntelliMed.Core.DTOs;
using IntelliMed.Core.Entities;
using IntelliMed.Infrastructure.Data;
using IntelliMed.Infrastructure.Repositories;
using IntelliMed.Tests.Helpers;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace IntelliMed.Tests.Repositories;

public class AppointmentRepositoryTests : IDisposable
{
    private readonly AppointmentRepository _repository;
    private readonly AppDbContext _context;

    public AppointmentRepositoryTests()
    {
        _context = TestDbContextFactory.CreateInMemoryContext();
        _repository = new AppointmentRepository(_context);
    }

    public void Dispose()
    {
        _context.Dispose();
    }

    [Fact]
    public async Task CreateAsync_WithValidDto_ReturnsNewAppointmentId()
    {
        // Arrange
        var patient = new Patient
        {
            FirstName = "Test",
            LastName = "Patient",
            Email = "test@example.com",
            IsActive = true
        };
        var practitioner = new Practitioner
        {
            FirstName = "Dr",
            LastName = "Smith",
            Email = "dr.smith@example.com",
            IsActive = true
        };
        _context.Patients.Add(patient);
        _context.Practitioners.Add(practitioner);
        await _context.SaveChangesAsync();

        var dto = new CreateAppointmentDto
        {
            PatientId = patient.Id,
            PractitionerId = practitioner.Id,
            AppointmentDate = DateTime.Today.AddDays(1),
            StartTime = TimeSpan.FromHours(9),
            EndTime = TimeSpan.FromHours(10),
            Type = AppointmentType.Standard,
            Notes = "Initial consultation"
        };

        // Act
        var result = await _repository.CreateAsync(dto);

        // Assert
        result.Should().BeGreaterThan(0);
        var appointment = await _context.Appointments.FindAsync(result);
        appointment.Should().NotBeNull();
        appointment!.PatientId.Should().Be(patient.Id);
        appointment.PractitionerId.Should().Be(practitioner.Id);
    }

    [Fact]
    public async Task GetByIdAsync_WithExistingAppointment_ReturnsAppointmentDto()
    {
        // Arrange
        var patient = new Patient
        {
            FirstName = "Test",
            LastName = "Patient",
            Email = "test@example.com",
            IsActive = true
        };
        var practitioner = new Practitioner
        {
            FirstName = "Dr",
            LastName = "Smith",
            Email = "dr.smith@example.com",
            IsActive = true
        };
        _context.Patients.Add(patient);
        _context.Practitioners.Add(practitioner);
        await _context.SaveChangesAsync();

        var appointment = new Appointment
        {
            PatientId = patient.Id,
            PractitionerId = practitioner.Id,
            AppointmentDate = DateTime.Today.AddDays(1),
            StartTime = TimeSpan.FromHours(10),
            EndTime = TimeSpan.FromHours(11),
            Status = AppointmentStatus.Scheduled,
            Type = AppointmentType.Standard
        };
        _context.Appointments.Add(appointment);
        await _context.SaveChangesAsync();

        // Act
        var result = await _repository.GetByIdAsync(appointment.Id);

        // Assert
        result.Should().NotBeNull();
        result!.PatientName.Should().Contain("Test");
        result.PractitionerName.Should().Contain("Smith");
    }

    [Fact]
    public async Task GetByIdAsync_WithNonExistingAppointment_ReturnsNull()
    {
        // Act
        var result = await _repository.GetByIdAsync(999);

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public async Task UpdateAsync_WithExistingAppointment_UpdatesAppointment()
    {
        // Arrange
        var patient = new Patient
        {
            FirstName = "Test",
            LastName = "Patient",
            Email = "test@example.com",
            IsActive = true
        };
        var practitioner = new Practitioner
        {
            FirstName = "Dr",
            LastName = "Smith",
            Email = "dr.smith@example.com",
            IsActive = true
        };
        _context.Patients.Add(patient);
        _context.Practitioners.Add(practitioner);
        await _context.SaveChangesAsync();

        var appointment = new Appointment
        {
            PatientId = patient.Id,
            PractitionerId = practitioner.Id,
            AppointmentDate = DateTime.Today.AddDays(1),
            StartTime = TimeSpan.FromHours(9),
            EndTime = TimeSpan.FromHours(10),
            Status = AppointmentStatus.Scheduled,
            Type = AppointmentType.Standard
        };
        _context.Appointments.Add(appointment);
        await _context.SaveChangesAsync();

        var updateDto = new UpdateAppointmentDto
        {
            PatientId = patient.Id,
            PractitionerId = practitioner.Id,
            AppointmentDate = DateTime.Today.AddDays(2),
            StartTime = TimeSpan.FromHours(14),
            EndTime = TimeSpan.FromHours(15),
            Status = AppointmentStatus.Completed,
            Type = AppointmentType.Standard
        };

        // Act
        await _repository.UpdateAsync(appointment.Id, updateDto);

        // Assert
        var updated = await _context.Appointments.FindAsync(appointment.Id);
        updated!.Status.Should().Be(AppointmentStatus.Completed);
        updated.Type.Should().Be(AppointmentType.Standard);
    }

    [Fact]
    public async Task UpdateAsync_WithNonExistingAppointment_ThrowsException()
    {
        // Arrange
        var updateDto = new UpdateAppointmentDto
        {
            PatientId = 1,
            PractitionerId = 1,
            AppointmentDate = DateTime.Today,
            StartTime = TimeSpan.FromHours(9),
            EndTime = TimeSpan.FromHours(10),
            Status = AppointmentStatus.Scheduled,
            Type = AppointmentType.Standard
        };

        // Act & Assert
        await Assert.ThrowsAsync<InvalidOperationException>(
            () => _repository.UpdateAsync(999, updateDto));
    }

    [Fact]
    public async Task SearchAsync_WithPatientIdFilter_ReturnsMatchingAppointments()
    {
        // Arrange
        var patient1 = new Patient { FirstName = "Patient", LastName = "One", Email = "p1@example.com", IsActive = true };
        var patient2 = new Patient { FirstName = "Patient", LastName = "Two", Email = "p2@example.com", IsActive = true };
        var practitioner = new Practitioner { FirstName = "Dr", LastName = "Smith", Email = "dr@example.com", IsActive = true };
        _context.Patients.AddRange(patient1, patient2);
        _context.Practitioners.Add(practitioner);
        await _context.SaveChangesAsync();

        var appointments = new[]
        {
            new Appointment { PatientId = patient1.Id, PractitionerId = practitioner.Id, AppointmentDate = DateTime.Today, StartTime = TimeSpan.FromHours(9), EndTime = TimeSpan.FromHours(10), Status = AppointmentStatus.Scheduled, Type = AppointmentType.Standard },
            new Appointment { PatientId = patient2.Id, PractitionerId = practitioner.Id, AppointmentDate = DateTime.Today, StartTime = TimeSpan.FromHours(10), EndTime = TimeSpan.FromHours(11), Status = AppointmentStatus.Scheduled, Type = AppointmentType.Standard },
            new Appointment { PatientId = patient1.Id, PractitionerId = practitioner.Id, AppointmentDate = DateTime.Today.AddDays(1), StartTime = TimeSpan.FromHours(11), EndTime = TimeSpan.FromHours(12), Status = AppointmentStatus.Scheduled, Type = AppointmentType.Standard }
        };
        _context.Appointments.AddRange(appointments);
        await _context.SaveChangesAsync();

        var search = new AppointmentSearchDto { PatientId = patient1.Id };

        // Act
        var result = (await _repository.SearchAsync(search)).ToList();

        // Assert
        result.Should().HaveCount(2);
        result.Should().OnlyContain(a => a.PatientId == patient1.Id);
    }

    [Fact]
    public async Task SearchAsync_WithDateRange_ReturnsAppointmentsInRange()
    {
        // Arrange
        var patient = new Patient { FirstName = "Test", LastName = "Patient", Email = "test@example.com", IsActive = true };
        var practitioner = new Practitioner { FirstName = "Dr", LastName = "Smith", Email = "dr@example.com", IsActive = true };
        _context.Patients.Add(patient);
        _context.Practitioners.Add(practitioner);
        await _context.SaveChangesAsync();

        var appointments = new[]
        {
            new Appointment { PatientId = patient.Id, PractitionerId = practitioner.Id, AppointmentDate = DateTime.Today.AddDays(-5), StartTime = TimeSpan.FromHours(9), EndTime = TimeSpan.FromHours(10), Status = AppointmentStatus.Scheduled, Type = AppointmentType.Standard },
            new Appointment { PatientId = patient.Id, PractitionerId = practitioner.Id, AppointmentDate = DateTime.Today, StartTime = TimeSpan.FromHours(10), EndTime = TimeSpan.FromHours(11), Status = AppointmentStatus.Scheduled, Type = AppointmentType.Standard },
            new Appointment { PatientId = patient.Id, PractitionerId = practitioner.Id, AppointmentDate = DateTime.Today.AddDays(5), StartTime = TimeSpan.FromHours(11), EndTime = TimeSpan.FromHours(12), Status = AppointmentStatus.Scheduled, Type = AppointmentType.Standard }
        };
        _context.Appointments.AddRange(appointments);
        await _context.SaveChangesAsync();

        var search = new AppointmentSearchDto
        {
            FromDate = DateTime.Today.AddDays(-1),
            ToDate = DateTime.Today.AddDays(1)
        };

        // Act
        var result = (await _repository.SearchAsync(search)).ToList();

        // Assert
        result.Should().HaveCount(1);
        result[0].AppointmentDate.Should().Be(DateTime.Today);
    }

    [Fact]
    public async Task SearchAsync_WithStatusFilter_ReturnsMatchingAppointments()
    {
        // Arrange
        var patient = new Patient { FirstName = "Test", LastName = "Patient", Email = "test@example.com", IsActive = true };
        var practitioner = new Practitioner { FirstName = "Dr", LastName = "Smith", Email = "dr@example.com", IsActive = true };
        _context.Patients.Add(patient);
        _context.Practitioners.Add(practitioner);
        await _context.SaveChangesAsync();

        var appointments = new[]
        {
            new Appointment { PatientId = patient.Id, PractitionerId = practitioner.Id, AppointmentDate = DateTime.Today, StartTime = TimeSpan.FromHours(9), EndTime = TimeSpan.FromHours(10), Status = AppointmentStatus.Scheduled, Type = AppointmentType.Standard },
            new Appointment { PatientId = patient.Id, PractitionerId = practitioner.Id, AppointmentDate = DateTime.Today, StartTime = TimeSpan.FromHours(10), EndTime = TimeSpan.FromHours(11), Status = AppointmentStatus.Completed, Type = AppointmentType.Standard },
            new Appointment { PatientId = patient.Id, PractitionerId = practitioner.Id, AppointmentDate = DateTime.Today, StartTime = TimeSpan.FromHours(11), EndTime = TimeSpan.FromHours(12), Status = AppointmentStatus.Cancelled, Type = AppointmentType.Standard }
        };
        _context.Appointments.AddRange(appointments);
        await _context.SaveChangesAsync();

        var search = new AppointmentSearchDto { Status = AppointmentStatus.Completed };

        // Act
        var result = (await _repository.SearchAsync(search)).ToList();

        // Assert
        result.Should().HaveCount(1);
        result[0].Status.Should().Be(AppointmentStatus.Completed);
    }

    [Fact]
    public async Task GetPagedAsync_ReturnsCorrectPage()
    {
        // Arrange
        var patient = new Patient { FirstName = "Test", LastName = "Patient", Email = "test@example.com", IsActive = true };
        var practitioner = new Practitioner { FirstName = "Dr", LastName = "Smith", Email = "dr@example.com", IsActive = true };
        _context.Patients.Add(patient);
        _context.Practitioners.Add(practitioner);
        await _context.SaveChangesAsync();

        for (int i = 0; i < 25; i++)
        {
            _context.Appointments.Add(new Appointment
            {
                PatientId = patient.Id,
                PractitionerId = practitioner.Id,
                AppointmentDate = DateTime.Today.AddDays(i),
                StartTime = TimeSpan.FromHours(9),
                EndTime = TimeSpan.FromHours(10),
                Status = AppointmentStatus.Scheduled,
                Type = AppointmentType.Standard
            });
        }
        await _context.SaveChangesAsync();

        var search = new AppointmentSearchDto { Page = 2, PageSize = 10 };

        // Act
        var (items, totalCount) = await _repository.GetPagedAsync(search);

        // Assert
        totalCount.Should().Be(25);
        items.Should().HaveCount(10);
    }

    [Fact]
    public async Task DeleteAsync_RemovesAppointment()
    {
        // Arrange
        var patient = new Patient { FirstName = "Test", LastName = "Patient", Email = "test@example.com", IsActive = true };
        var practitioner = new Practitioner { FirstName = "Dr", LastName = "Smith", Email = "dr@example.com", IsActive = true };
        _context.Patients.Add(patient);
        _context.Practitioners.Add(practitioner);
        await _context.SaveChangesAsync();

        var appointment = new Appointment
        {
            PatientId = patient.Id,
            PractitionerId = practitioner.Id,
            AppointmentDate = DateTime.Today,
            StartTime = TimeSpan.FromHours(9),
            EndTime = TimeSpan.FromHours(10),
            Status = AppointmentStatus.Scheduled,
            Type = AppointmentType.Standard
        };
        _context.Appointments.Add(appointment);
        await _context.SaveChangesAsync();
        var appointmentId = appointment.Id;

        // Act
        await _repository.DeleteAsync(appointmentId);

        // Assert
        var deleted = await _context.Appointments.FindAsync(appointmentId);
        deleted.Should().BeNull();
    }

    [Fact]
    public async Task ExistsAsync_WithExistingAppointment_ReturnsTrue()
    {
        // Arrange
        var patient = new Patient { FirstName = "Test", LastName = "Patient", Email = "test@example.com", IsActive = true };
        var practitioner = new Practitioner { FirstName = "Dr", LastName = "Smith", Email = "dr@example.com", IsActive = true };
        _context.Patients.Add(patient);
        _context.Practitioners.Add(practitioner);
        await _context.SaveChangesAsync();

        var appointment = new Appointment
        {
            PatientId = patient.Id,
            PractitionerId = practitioner.Id,
            AppointmentDate = DateTime.Today,
            StartTime = TimeSpan.FromHours(9),
            EndTime = TimeSpan.FromHours(10),
            Status = AppointmentStatus.Scheduled,
            Type = AppointmentType.Standard
        };
        _context.Appointments.Add(appointment);
        await _context.SaveChangesAsync();

        // Act
        var result = await _repository.ExistsAsync(appointment.Id);

        // Assert
        result.Should().BeTrue();
    }

    [Fact]
    public async Task ExistsAsync_WithNonExistingAppointment_ReturnsFalse()
    {
        // Act
        var result = await _repository.ExistsAsync(999);

        // Assert
        result.Should().BeFalse();
    }
}