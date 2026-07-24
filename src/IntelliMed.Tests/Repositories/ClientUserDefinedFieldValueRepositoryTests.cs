using FluentAssertions;
using IntelliMed.Core.DTOs;
using IntelliMed.Core.Entities;
using IntelliMed.Infrastructure.Data;
using IntelliMed.Infrastructure.Repositories;
using IntelliMed.Tests.Helpers;
using Xunit;

namespace IntelliMed.Tests.Repositories;

public class ClientUserDefinedFieldValueRepositoryTests : IDisposable
{
    private readonly ClientUserDefinedFieldValueRepository _repository;
    private readonly AppDbContext _context;
    private readonly int _clientId;
    private readonly int _fieldTypeId;

    public ClientUserDefinedFieldValueRepositoryTests()
    {
        _context = TestDbContextFactory.CreateInMemoryContext();
        _repository = new ClientUserDefinedFieldValueRepository(_context);

        var client = new Client { FirstName = "Udf", LastName = "Test" };
        var fieldType = new UserDefinedFieldType { Name = "Preferred Pharmacy", IsActive = true };
        _context.Clients.Add(client);
        _context.UserDefinedFieldTypes.Add(fieldType);
        _context.SaveChanges();
        _clientId = client.Id;
        _fieldTypeId = fieldType.Id;
    }

    public void Dispose() => _context.Dispose();

    [Fact]
    public async Task CreateAsync_WithValidDto_ReturnsNewValueId()
    {
        var dto = new CreateClientUserDefinedFieldValueDto
        {
            ClientId = _clientId,
            UserDefinedFieldTypeId = _fieldTypeId,
            Value = "Chemist Warehouse"
        };

        var result = await _repository.CreateAsync(dto);

        result.Should().BeGreaterThan(0);
        var value = await _context.ClientUserDefinedFieldValues.FindAsync(result);
        value!.Value.Should().Be("Chemist Warehouse");
    }

    [Fact]
    public async Task GetByClientIdAsync_IncludesFieldNameAndType()
    {
        _context.ClientUserDefinedFieldValues.Add(new ClientUserDefinedFieldValue
        {
            ClientId = _clientId,
            UserDefinedFieldTypeId = _fieldTypeId,
            Value = "Some Value"
        });
        await _context.SaveChangesAsync();

        var result = (await _repository.GetByClientIdAsync(_clientId)).ToList();

        result.Should().ContainSingle();
        result[0].FieldName.Should().Be("Preferred Pharmacy");
        result[0].Value.Should().Be("Some Value");
    }
}
