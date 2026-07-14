using FluentAssertions;
using IntelliMed.Core.DTOs;
using IntelliMed.Core.Entities;
using IntelliMed.Infrastructure.Data;
using IntelliMed.Infrastructure.Repositories;
using IntelliMed.Tests.Helpers;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace IntelliMed.Tests.Repositories;

public class PatientRepositoryTests : IDisposable
{
    private readonly PatientRepository _repository;
    private readonly AppDbContext _context;

    public PatientRepositoryTests()
    {
        _context = TestDbContextFactory.CreateInMemoryContext();
        _repository = new PatientRepository(_context);
    }

    public void Dispose()
    {
        _context.Dispose();
    }

    [Fact]
    public async Task CreateAsync_WithValidDto_ReturnsNewPatientId()
    {
        // Arrange
        var dto = new CreatePatientDto
        {
            FirstName = "John",
            LastName = "Doe",
            Email = "john.doe@example.com",
            Phone = "0412345678",
            DateOfBirth = new DateTime(1990, 5, 15),
            Address = "123 Main Street",
            MedicareNumber = "1234567890"
        };

        // Act
        var result = await _repository.CreateAsync(dto);

        // Assert
        result.Should().BeGreaterThan(0);
        var patient = await _context.Patients.FindAsync(result);
        patient.Should().NotBeNull();
        patient!.FirstName.Should().Be("John");
        patient.LastName.Should().Be("Doe");
        patient.Email.Should().Be("john.doe@example.com");
    }

    [Fact]
    public async Task GetByIdAsync_WithExistingPatient_ReturnsPatientDto()
    {
        // Arrange
        var patient = new Patient
        {
            FirstName = "Jane",
            LastName = "Smith",
            Email = "jane.smith@example.com",
            Phone = "0498765432",
            DateOfBirth = new DateTime(1985, 3, 20),
            IsActive = true
        };
        _context.Patients.Add(patient);
        await _context.SaveChangesAsync();

        // Act
        var result = await _repository.GetByIdAsync(patient.Id);

        // Assert
        result.Should().NotBeNull();
        result!.FirstName.Should().Be("Jane");
        result.LastName.Should().Be("Smith");
        result.Email.Should().Be("jane.smith@example.com");
    }

    [Fact]
    public async Task GetByIdAsync_WithNonExistingPatient_ReturnsNull()
    {
        // Act
        var result = await _repository.GetByIdAsync(999);

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public async Task UpdateAsync_WithExistingPatient_UpdatesPatient()
    {
        // Arrange
        var patient = new Patient
        {
            FirstName = "Original",
            LastName = "Name",
            Email = "original@example.com",
            Phone = "0411111111",
            IsActive = true
        };
        _context.Patients.Add(patient);
        await _context.SaveChangesAsync();

        var updateDto = new UpdatePatientDto
        {
            FirstName = "Updated",
            LastName = "Name",
            Email = "updated@example.com",
            Phone = "0422222222"
        };

        // Act
        await _repository.UpdateAsync(patient.Id, updateDto);

        // Assert
        var updatedPatient = await _context.Patients.FindAsync(patient.Id);
        updatedPatient!.FirstName.Should().Be("Updated");
        updatedPatient.Email.Should().Be("updated@example.com");
    }

    [Fact]
    public async Task UpdateAsync_WithNonExistingPatient_ThrowsException()
    {
        // Arrange
        var updateDto = new UpdatePatientDto
        {
            FirstName = "Test",
            LastName = "User"
        };

        // Act & Assert
        await Assert.ThrowsAsync<InvalidOperationException>(
            () => _repository.UpdateAsync(999, updateDto));
    }

    [Fact]
    public async Task SearchAsync_WithNameQuery_ReturnsMatchingPatients()
    {
        // Arrange
        var patients = new[]
        {
            new Patient { FirstName = "John", LastName = "Doe", Email = "john@example.com", IsActive = true },
            new Patient { FirstName = "John", LastName = "Smith", Email = "john.smith@example.com", IsActive = true },
            new Patient { FirstName = "Jane", LastName = "Doe", Email = "jane@example.com", IsActive = true }
        };
        _context.Patients.AddRange(patients);
        await _context.SaveChangesAsync();

        var search = new PatientSearchDto { Query = "John" };

        // Act
        var result = (await _repository.SearchAsync(search)).ToList();

        // Assert
        result.Should().HaveCount(2);
        result.Should().Contain(p => p.LastName == "Doe");
        result.Should().Contain(p => p.LastName == "Smith");
    }

    [Fact]
    public async Task SearchAsync_WithEmailQuery_ReturnsMatchingPatients()
    {
        // Arrange
        var patients = new[]
        {
            new Patient { FirstName = "Test", LastName = "User1", Email = "test1@example.com", IsActive = true },
            new Patient { FirstName = "Test", LastName = "User2", Email = "test2@example.com", IsActive = true },
            new Patient { FirstName = "Other", LastName = "Person", Email = "other@example.com", IsActive = true }
        };
        _context.Patients.AddRange(patients);
        await _context.SaveChangesAsync();

        var search = new PatientSearchDto { Query = "test1@example.com" };

        // Act
        var result = (await _repository.SearchAsync(search)).ToList();

        // Assert
        result.Should().HaveCount(1);
        result[0].Email.Should().Be("test1@example.com");
    }

    [Fact]
    public async Task SearchAsync_WithActiveFilter_ReturnsOnlyActivePatients()
    {
        // Arrange
        var patients = new[]
        {
            new Patient { FirstName = "Active", LastName = "Patient", Email = "active@example.com", IsActive = true },
            new Patient { FirstName = "Inactive", LastName = "Patient", Email = "inactive@example.com", IsActive = false }
        };
        _context.Patients.AddRange(patients);
        await _context.SaveChangesAsync();

        var search = new PatientSearchDto { IsActive = true };

        // Act
        var result = (await _repository.SearchAsync(search)).ToList();

        // Assert
        result.Should().HaveCount(1);
        result[0].FirstName.Should().Be("Active");
    }

    [Fact]
    public async Task GetPagedAsync_ReturnsCorrectPage()
    {
        // Arrange
        for (int i = 1; i <= 25; i++)
        {
            _context.Patients.Add(new Patient
            {
                FirstName = $"Patient{i:D2}",
                LastName = "Test",
                Email = $"patient{i}@example.com",
                IsActive = true
            });
        }
        await _context.SaveChangesAsync();

        var search = new PatientSearchDto { Page = 2, PageSize = 10 };

        // Act
        var (items, totalCount) = await _repository.GetPagedAsync(search);

        // Assert
        totalCount.Should().Be(25);
        items.Should().HaveCount(10);
    }

    [Fact]
    public async Task GetAllAsync_ReturnsAllPatients()
    {
        // Arrange
        var patients = new[]
        {
            new Patient { FirstName = "Patient1", LastName = "Test", Email = "p1@example.com", IsActive = true },
            new Patient { FirstName = "Patient2", LastName = "Test", Email = "p2@example.com", IsActive = true }
        };
        _context.Patients.AddRange(patients);
        await _context.SaveChangesAsync();

        // Act
        var result = (await _repository.GetAllAsync()).ToList();

        // Assert
        result.Should().HaveCount(2);
    }

    [Fact]
    public async Task DeleteAsync_RemovesPatient()
    {
        // Arrange
        var patient = new Patient
        {
            FirstName = "ToDelete",
            LastName = "Patient",
            Email = "delete@example.com",
            IsActive = true
        };
        _context.Patients.Add(patient);
        await _context.SaveChangesAsync();
        var patientId = patient.Id;

        // Act
        await _repository.DeleteAsync(patientId);

        // Assert
        var deletedPatient = await _context.Patients.FindAsync(patientId);
        deletedPatient.Should().BeNull();
    }

    [Fact]
    public async Task ExistsAsync_WithExistingPatient_ReturnsTrue()
    {
        // Arrange
        var patient = new Patient
        {
            FirstName = "Exists",
            LastName = "Patient",
            Email = "exists@example.com",
            IsActive = true
        };
        _context.Patients.Add(patient);
        await _context.SaveChangesAsync();

        // Act
        var result = await _repository.ExistsAsync(patient.Id);

        // Assert
        result.Should().BeTrue();
    }

    [Fact]
    public async Task ExistsAsync_WithNonExistingPatient_ReturnsFalse()
    {
        // Act
        var result = await _repository.ExistsAsync(999);

        // Assert
        result.Should().BeFalse();
    }
}