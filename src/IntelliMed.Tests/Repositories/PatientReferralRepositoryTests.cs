using FluentAssertions;
using IntelliMed.Core.DTOs;
using IntelliMed.Core.Entities;
using IntelliMed.Infrastructure.Data;
using IntelliMed.Infrastructure.Repositories;
using IntelliMed.Tests.Helpers;
using Xunit;

namespace IntelliMed.Tests.Repositories;

public class PatientReferralRepositoryTests : IDisposable
{
    private readonly PatientReferralRepository _repository;
    private readonly AppDbContext _context;
    private readonly int _patientId;

    public PatientReferralRepositoryTests()
    {
        _context = TestDbContextFactory.CreateInMemoryContext();
        _repository = new PatientReferralRepository(_context);

        var patient = new Patient { FirstName = "Ref", LastName = "Test" };
        _context.Patients.Add(patient);
        _context.SaveChanges();
        _patientId = patient.Id;
    }

    public void Dispose() => _context.Dispose();

    [Fact]
    public async Task CreateAsync_WithValidDto_ReturnsNewReferralId()
    {
        var dto = new CreatePatientReferralDto
        {
            PatientId = _patientId,
            ReferralDate = new DateTime(2026, 1, 1),
            ReferringProviderName = "Dr Smith",
            IsGP = true
        };

        var result = await _repository.CreateAsync(dto);

        result.Should().BeGreaterThan(0);
        var referral = await _context.PatientReferrals.FindAsync(result);
        referral.Should().NotBeNull();
        referral!.ReferringProviderName.Should().Be("Dr Smith");
        referral.IsGP.Should().BeTrue();
    }

    [Fact]
    public async Task GetByPatientIdAsync_ReturnsOnlyThatPatientsReferrals()
    {
        var other = new Patient { FirstName = "Other", LastName = "Patient" };
        _context.Patients.Add(other);
        await _context.SaveChangesAsync();

        _context.PatientReferrals.AddRange(
            new PatientReferral { PatientId = _patientId, ReferralDate = DateTime.Today, ReferringProviderName = "A" },
            new PatientReferral { PatientId = _patientId, ReferralDate = DateTime.Today, ReferringProviderName = "B" },
            new PatientReferral { PatientId = other.Id, ReferralDate = DateTime.Today, ReferringProviderName = "C" });
        await _context.SaveChangesAsync();

        var result = (await _repository.GetByPatientIdAsync(_patientId)).ToList();

        result.Should().HaveCount(2);
        result.Should().OnlyContain(r => r.PatientId == _patientId);
    }

    [Fact]
    public async Task ArchiveAsync_SetsIsArchivedTrue()
    {
        var referral = new PatientReferral { PatientId = _patientId, ReferralDate = DateTime.Today, ReferringProviderName = "A" };
        _context.PatientReferrals.Add(referral);
        await _context.SaveChangesAsync();

        await _repository.ArchiveAsync(referral.Id);

        var updated = await _context.PatientReferrals.FindAsync(referral.Id);
        updated!.IsArchived.Should().BeTrue();
    }

    [Fact]
    public async Task ArchiveAsync_WithNonExistingReferral_ThrowsException()
    {
        await Assert.ThrowsAsync<InvalidOperationException>(() => _repository.ArchiveAsync(999));
    }

    [Fact]
    public async Task UpdateAsync_WithExistingReferral_UpdatesFields()
    {
        var referral = new PatientReferral { PatientId = _patientId, ReferralDate = DateTime.Today, ReferringProviderName = "Old" };
        _context.PatientReferrals.Add(referral);
        await _context.SaveChangesAsync();

        var update = new UpdatePatientReferralDto
        {
            ReferralDate = referral.ReferralDate,
            ReferringProviderName = "New",
            IsGP = true
        };

        await _repository.UpdateAsync(referral.Id, update);

        var updated = await _context.PatientReferrals.FindAsync(referral.Id);
        updated!.ReferringProviderName.Should().Be("New");
        updated.IsGP.Should().BeTrue();
    }
}
