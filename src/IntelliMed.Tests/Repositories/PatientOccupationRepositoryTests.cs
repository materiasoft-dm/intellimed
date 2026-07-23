using FluentAssertions;
using IntelliMed.Core.DTOs;
using IntelliMed.Core.Entities;
using IntelliMed.Infrastructure.Data;
using IntelliMed.Infrastructure.Repositories;
using IntelliMed.Tests.Helpers;
using Xunit;

namespace IntelliMed.Tests.Repositories;

public class PatientOccupationRepositoryTests : IDisposable
{
    private readonly PatientOccupationRepository _repository;
    private readonly AppDbContext _context;
    private readonly int _patientId;

    public PatientOccupationRepositoryTests()
    {
        _context = TestDbContextFactory.CreateInMemoryContext();
        _repository = new PatientOccupationRepository(_context);

        var patient = new Patient { FirstName = "Occ", LastName = "Test" };
        _context.Patients.Add(patient);
        _context.SaveChanges();
        _patientId = patient.Id;
    }

    public void Dispose() => _context.Dispose();

    [Fact]
    public async Task CreateAsync_WithValidDto_ReturnsNewOccupationId()
    {
        var dto = new CreatePatientOccupationDto
        {
            PatientId = _patientId,
            Occupation = "Carpenter",
            HasAsbestos = true
        };

        var result = await _repository.CreateAsync(dto);

        result.Should().BeGreaterThan(0);
        var occupation = await _context.PatientOccupations.FindAsync(result);
        occupation!.Occupation.Should().Be("Carpenter");
        occupation.HasAsbestos.Should().BeTrue();
    }

    [Fact]
    public async Task ArchiveAsync_SetsIsArchivedTrue()
    {
        var occupation = new PatientOccupation { PatientId = _patientId, Occupation = "Miner" };
        _context.PatientOccupations.Add(occupation);
        await _context.SaveChangesAsync();

        await _repository.ArchiveAsync(occupation.Id);

        var updated = await _context.PatientOccupations.FindAsync(occupation.Id);
        updated!.IsArchived.Should().BeTrue();
    }
}
