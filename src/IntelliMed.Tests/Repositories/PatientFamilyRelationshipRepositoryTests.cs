using FluentAssertions;
using IntelliMed.Core.DTOs;
using IntelliMed.Core.Entities;
using IntelliMed.Infrastructure.Data;
using IntelliMed.Infrastructure.Repositories;
using IntelliMed.Tests.Helpers;
using Xunit;

namespace IntelliMed.Tests.Repositories;

public class PatientFamilyRelationshipRepositoryTests : IDisposable
{
    private readonly PatientFamilyRelationshipRepository _repository;
    private readonly AppDbContext _context;
    private readonly int _patientId;
    private readonly int _relativeId;

    public PatientFamilyRelationshipRepositoryTests()
    {
        _context = TestDbContextFactory.CreateInMemoryContext();
        _repository = new PatientFamilyRelationshipRepository(_context);

        var patient = new Patient { FirstName = "Head", LastName = "OfFamily", Address = "1 St", Suburb = "Town", State = "VIC", Postcode = "3000" };
        var relative = new Patient { FirstName = "Child", LastName = "OfFamily" };
        _context.Patients.AddRange(patient, relative);
        _context.SaveChanges();
        _patientId = patient.Id;
        _relativeId = relative.Id;
    }

    public void Dispose() => _context.Dispose();

    [Fact]
    public async Task CreateAsync_WithValidDto_ReturnsNewRelationshipId()
    {
        var dto = new CreatePatientFamilyRelationshipDto
        {
            PatientId = _patientId,
            RelativePatientId = _relativeId,
            RelationshipType = "Child"
        };

        var result = await _repository.CreateAsync(dto);

        result.Should().BeGreaterThan(0);
        var relationship = await _context.PatientFamilyRelationships.FindAsync(result);
        relationship!.RelativePatientId.Should().Be(_relativeId);
    }

    [Fact]
    public async Task GetByPatientIdAsync_ReturnsRelativeNameAndAddress()
    {
        _context.PatientFamilyRelationships.Add(new PatientFamilyRelationship
        {
            PatientId = _patientId,
            RelativePatientId = _relativeId,
            RelationshipType = "Child"
        });
        await _context.SaveChangesAsync();

        var result = (await _repository.GetByPatientIdAsync(_patientId)).ToList();

        result.Should().ContainSingle();
        result[0].RelativeName.Should().Be("Child OfFamily");
        result[0].RelationshipType.Should().Be("Child");
    }
}
