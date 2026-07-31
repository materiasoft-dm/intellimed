using FluentAssertions;
using IntelliMed.Core.DTOs;
using IntelliMed.Core.Entities;
using IntelliMed.Infrastructure.Data;
using IntelliMed.Infrastructure.Repositories;
using IntelliMed.Tests.Helpers;
using Xunit;

namespace IntelliMed.Tests.Repositories;

public class SearchActionRepositoryTests : IDisposable
{
    private readonly SearchActionRepository _repository;
    private readonly AppDbContext _context;

    public SearchActionRepositoryTests()
    {
        _context = TestDbContextFactory.CreateInMemoryContext();
        _repository = new SearchActionRepository(_context);
    }

    public void Dispose() => _context.Dispose();

    private static SaveSearchActionRequest MakeRequest(string title = "Add Client", string? pageKey = "clients/create") => new()
    {
        Title = title,
        Keywords = "create new patient register",
        Description = "Create a new client record",
        Category = "Clinical",
        ActionType = SearchActionType.Navigate,
        Target = "/clients/add",
        PageKey = pageKey,
        SortOrder = 10
    };

    [Fact]
    public async Task CreateAsync_WithValidRequest_ReturnsNewActionId()
    {
        var result = await _repository.CreateAsync(MakeRequest());

        result.Should().BeGreaterThan(0);
        var action = await _context.SearchActions.FindAsync(result);
        action!.Title.Should().Be("Add Client");
        action.PageKey.Should().Be("clients/create");
        action.IsActive.Should().BeTrue();
    }

    [Fact]
    public async Task GetAllActiveAsync_ExcludesArchivedActions()
    {
        _context.SearchActions.AddRange(
            new SearchAction { Title = "Active Action", Category = "General", Target = "/", IsActive = true, SortOrder = 1 },
            new SearchAction { Title = "Archived Action", Category = "General", Target = "/", IsActive = false, SortOrder = 2 });
        await _context.SaveChangesAsync();

        var result = (await _repository.GetAllActiveAsync()).ToList();

        result.Should().ContainSingle(a => a.Title == "Active Action");
        result.Should().NotContain(a => a.Title == "Archived Action");
    }

    [Fact]
    public async Task UpdateAsync_ChangesFields()
    {
        var id = await _repository.CreateAsync(MakeRequest());

        await _repository.UpdateAsync(id, MakeRequest(title: "Add Client (renamed)", pageKey: null));

        var updated = await _repository.GetByIdAsync(id);
        updated!.Title.Should().Be("Add Client (renamed)");
        updated.PageKey.Should().BeNull();
    }

    [Fact]
    public async Task ArchiveAsync_SetsIsActiveFalse_AndHidesFromGetAllActive()
    {
        var id = await _repository.CreateAsync(MakeRequest());

        await _repository.ArchiveAsync(id);

        var all = (await _repository.GetAllActiveAsync()).ToList();
        all.Should().BeEmpty();
        var raw = await _context.SearchActions.FindAsync(id);
        raw!.IsActive.Should().BeFalse();
    }
}
