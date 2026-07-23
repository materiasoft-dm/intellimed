using FluentAssertions;
using IntelliMed.Core.DTOs;
using IntelliMed.Core.Entities;
using IntelliMed.Infrastructure.Data;
using IntelliMed.Infrastructure.Repositories;
using IntelliMed.Tests.Helpers;
using Xunit;

namespace IntelliMed.Tests.Repositories;

public class UserDefinedFieldTypeRepositoryTests : IDisposable
{
    private readonly UserDefinedFieldTypeRepository _repository;
    private readonly AppDbContext _context;

    public UserDefinedFieldTypeRepositoryTests()
    {
        _context = TestDbContextFactory.CreateInMemoryContext();
        _repository = new UserDefinedFieldTypeRepository(_context);
    }

    public void Dispose() => _context.Dispose();

    [Fact]
    public async Task CreateAsync_WithValidDto_ReturnsNewFieldTypeId()
    {
        var dto = new CreateUserDefinedFieldTypeDto { Name = "Allergy Alert", FieldType = UdfFieldTypeEnum.Text };

        var result = await _repository.CreateAsync(dto);

        result.Should().BeGreaterThan(0);
        var fieldType = await _context.UserDefinedFieldTypes.FindAsync(result);
        fieldType!.Name.Should().Be("Allergy Alert");
    }

    [Fact]
    public async Task GetAllActiveAsync_ExcludesInactiveFieldTypes()
    {
        _context.UserDefinedFieldTypes.AddRange(
            new UserDefinedFieldType { Name = "Active Field", IsActive = true, DisplayOrder = 1 },
            new UserDefinedFieldType { Name = "Inactive Field", IsActive = false, DisplayOrder = 2 });
        await _context.SaveChangesAsync();

        var result = (await _repository.GetAllActiveAsync()).ToList();

        result.Should().ContainSingle(f => f.Name == "Active Field");
    }
}
