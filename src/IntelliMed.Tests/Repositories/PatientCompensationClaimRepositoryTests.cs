using FluentAssertions;
using IntelliMed.Core.DTOs;
using IntelliMed.Core.Entities;
using IntelliMed.Infrastructure.Data;
using IntelliMed.Infrastructure.Repositories;
using IntelliMed.Tests.Helpers;
using Xunit;

namespace IntelliMed.Tests.Repositories;

public class PatientCompensationClaimRepositoryTests : IDisposable
{
    private readonly PatientCompensationClaimRepository _repository;
    private readonly AppDbContext _context;
    private readonly int _patientId;

    public PatientCompensationClaimRepositoryTests()
    {
        _context = TestDbContextFactory.CreateInMemoryContext();
        _repository = new PatientCompensationClaimRepository(_context);

        var patient = new Patient { FirstName = "Claim", LastName = "Test" };
        _context.Patients.Add(patient);
        _context.SaveChanges();
        _patientId = patient.Id;
    }

    public void Dispose() => _context.Dispose();

    [Fact]
    public async Task CreateAsync_WithValidDto_ReturnsNewClaimId()
    {
        var dto = new CreatePatientCompensationClaimDto { PatientId = _patientId, ClaimNum = "WC-001" };

        var result = await _repository.CreateAsync(dto);

        result.Should().BeGreaterThan(0);
        var claim = await _context.PatientCompensationClaims.FindAsync(result);
        claim!.ClaimNum.Should().Be("WC-001");
    }

    [Fact]
    public async Task ArchiveAsync_SetsIsArchivedTrue()
    {
        var claim = new PatientCompensationClaim { PatientId = _patientId, ClaimNum = "WC-002" };
        _context.PatientCompensationClaims.Add(claim);
        await _context.SaveChangesAsync();

        await _repository.ArchiveAsync(claim.Id);

        var updated = await _context.PatientCompensationClaims.FindAsync(claim.Id);
        updated!.IsArchived.Should().BeTrue();
    }

    [Fact]
    public async Task GetByPatientIdAsync_ReturnsOnlyThatPatientsClaims()
    {
        _context.PatientCompensationClaims.Add(new PatientCompensationClaim { PatientId = _patientId, ClaimNum = "WC-003" });
        await _context.SaveChangesAsync();

        var result = (await _repository.GetByPatientIdAsync(_patientId)).ToList();

        result.Should().ContainSingle(c => c.ClaimNum == "WC-003");
    }
}
