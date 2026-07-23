using FluentAssertions;
using IntelliMed.Core.DTOs;
using IntelliMed.Core.Entities;
using IntelliMed.Infrastructure.Data;
using IntelliMed.Infrastructure.Repositories;
using IntelliMed.Tests.Helpers;
using Xunit;

namespace IntelliMed.Tests.Repositories;

public class PatientUserDefinedFieldValueRepositoryTests : IDisposable
{
    private readonly PatientUserDefinedFieldValueRepository _repository;
    private readonly AppDbContext _context;
    private readonly int _patientId;
    private readonly int _fieldTypeId;

    public PatientUserDefinedFieldValueRepositoryTests()
    {
        _context = TestDbContextFactory.CreateInMemoryContext();
        _repository = new PatientUserDefinedFieldValueRepository(_context);

        var patient = new Patient { FirstName = "Udf", LastName = "Test" };
        var fieldType = new UserDefinedFieldType { Name = "Preferred Pharmacy", IsActive = true };
        _context.Patients.Add(patient);
        _context.UserDefinedFieldTypes.Add(fieldType);
        _context.SaveChanges();
        _patientId = patient.Id;
        _fieldTypeId = fieldType.Id;
    }

    public void Dispose() => _context.Dispose();

    [Fact]
    public async Task CreateAsync_WithValidDto_ReturnsNewValueId()
    {
        var dto = new CreatePatientUserDefinedFieldValueDto
        {
            PatientId = _patientId,
            UserDefinedFieldTypeId = _fieldTypeId,
            Value = "Chemist Warehouse"
        };

        var result = await _repository.CreateAsync(dto);

        result.Should().BeGreaterThan(0);
        var value = await _context.PatientUserDefinedFieldValues.FindAsync(result);
        value!.Value.Should().Be("Chemist Warehouse");
    }

    [Fact]
    public async Task GetByPatientIdAsync_IncludesFieldNameAndType()
    {
        _context.PatientUserDefinedFieldValues.Add(new PatientUserDefinedFieldValue
        {
            PatientId = _patientId,
            UserDefinedFieldTypeId = _fieldTypeId,
            Value = "Some Value"
        });
        await _context.SaveChangesAsync();

        var result = (await _repository.GetByPatientIdAsync(_patientId)).ToList();

        result.Should().ContainSingle();
        result[0].FieldName.Should().Be("Preferred Pharmacy");
        result[0].Value.Should().Be("Some Value");
    }
}
