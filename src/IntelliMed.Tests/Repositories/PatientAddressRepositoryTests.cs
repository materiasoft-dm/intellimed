using FluentAssertions;
using IntelliMed.Core.DTOs;
using IntelliMed.Core.Entities;
using IntelliMed.Infrastructure.Data;
using IntelliMed.Infrastructure.Repositories;
using IntelliMed.Tests.Helpers;
using Xunit;

namespace IntelliMed.Tests.Repositories;

public class PatientAddressRepositoryTests : IDisposable
{
    private readonly PatientAddressRepository _repository;
    private readonly AppDbContext _context;
    private readonly int _patientId;

    public PatientAddressRepositoryTests()
    {
        _context = TestDbContextFactory.CreateInMemoryContext();
        _repository = new PatientAddressRepository(_context);

        var patient = new Patient { FirstName = "Addr", LastName = "Test" };
        _context.Patients.Add(patient);
        _context.SaveChanges();
        _patientId = patient.Id;
    }

    public void Dispose() => _context.Dispose();

    [Fact]
    public async Task CreateAsync_WithValidDto_ReturnsNewAddressId()
    {
        var dto = new CreatePatientAddressDto
        {
            PatientId = _patientId,
            AddressType = PatientAddressType.Postal,
            AddressLine1 = "PO Box 1",
            Suburb = "Sometown",
            Postcode = "3000",
            State = "VIC"
        };

        var result = await _repository.CreateAsync(dto);

        result.Should().BeGreaterThan(0);
        var address = await _context.PatientAddresses.FindAsync(result);
        address.Should().NotBeNull();
        address!.AddressType.Should().Be(PatientAddressType.Postal);
    }

    [Fact]
    public async Task GetByPatientIdAsync_ReturnsOnlyThatPatientsAddresses()
    {
        _context.PatientAddresses.Add(new PatientAddress
        {
            PatientId = _patientId,
            AddressType = PatientAddressType.Other,
            AddressLine1 = "1 Other St",
            Suburb = "X",
            Postcode = "1000",
            State = "NSW"
        });
        await _context.SaveChangesAsync();

        var result = (await _repository.GetByPatientIdAsync(_patientId)).ToList();

        result.Should().HaveCount(1);
        result[0].AddressType.Should().Be(PatientAddressType.Other);
    }

    [Fact]
    public async Task UpdateAsync_WithNonExistingAddress_ThrowsException()
    {
        var update = new UpdatePatientAddressDto { AddressLine1 = "x", Suburb = "x", Postcode = "x", State = "x" };
        await Assert.ThrowsAsync<InvalidOperationException>(() => _repository.UpdateAsync(999, update));
    }
}
