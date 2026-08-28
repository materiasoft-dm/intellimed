using FluentAssertions;
using IntelliMed.Core.DTOs;
using IntelliMed.Core.Entities;
using IntelliMed.Infrastructure.Data;
using IntelliMed.Infrastructure.Repositories;
using IntelliMed.Tests.Helpers;
using Xunit;

namespace IntelliMed.Tests.Repositories;

public class PractitionerRepositoryTests : IDisposable
{
    private readonly PractitionerRepository _repository;
    private readonly AppDbContext _context;

    public PractitionerRepositoryTests()
    {
        _context = TestDbContextFactory.CreateInMemoryContext();
        _repository = new PractitionerRepository(_context);
    }

    public void Dispose() => _context.Dispose();

    private async Task<int> AddPractitionerAsync(
        string firstName,
        string lastName,
        bool isActive = true,
        string? applicationUserId = null)
    {
        var practitioner = new Practitioner
        {
            FirstName = firstName,
            LastName = lastName,
            Title = "Dr",
            IsActive = isActive,
            ApplicationUserId = applicationUserId
        };
        _context.Practitioners.Add(practitioner);
        await _context.SaveChangesAsync();
        return practitioner.Id;
    }

    [Fact]
    public async Task GetAllActiveAsync_ExcludesInactivePractitioners()
    {
        await AddPractitionerAsync("Active", "Doctor");
        await AddPractitionerAsync("Retired", "Doctor", isActive: false);

        var results = await _repository.GetAllActiveAsync();

        results.Should().ContainSingle().Which.FirstName.Should().Be("Active");
    }

    [Fact]
    public async Task GetByIdAsync_ReturnsNullForUnknownId()
    {
        var result = await _repository.GetByIdAsync(9999);

        result.Should().BeNull();
    }

    [Fact]
    public async Task UpdateAsync_WithUnknownId_Throws()
    {
        var act = async () => await _repository.UpdateAsync(9999, new UpdatePractitionerDto
        {
            FirstName = "Ghost",
            LastName = "Practitioner"
        });

        await act.Should().ThrowAsync<InvalidOperationException>();
    }

    [Fact]
    public async Task GetByApplicationUserIdAsync_FindsTheLinkedPractitioner()
    {
        await AddPractitionerAsync("Linked", "Doctor", applicationUserId: "user-1");
        await AddPractitionerAsync("Unlinked", "Doctor");

        var result = await _repository.GetByApplicationUserIdAsync("user-1");

        result.Should().NotBeNull();
        result!.FirstName.Should().Be("Linked");
    }

    [Fact]
    public async Task GetByApplicationUserIdAsync_ReturnsNullWhenNoPractitionerIsLinked()
    {
        await AddPractitionerAsync("Unlinked", "Doctor");

        var result = await _repository.GetByApplicationUserIdAsync("nobody");

        result.Should().BeNull();
    }

    [Fact]
    public async Task SetLinkedUserAsync_LinksTheUserToThePractitioner()
    {
        var id = await AddPractitionerAsync("Target", "Doctor");

        await _repository.SetLinkedUserAsync(id, "user-1");

        var linked = await _context.Practitioners.FindAsync(id);
        linked!.ApplicationUserId.Should().Be("user-1");
    }

    [Fact]
    public async Task SetLinkedUserAsync_MovesTheLink_UnlinkingAnyPreviousHolder()
    {
        // One login must map to at most one practitioner — per-provider patient visibility keys off
        // this, so a stale second link would widen what that user can see.
        var previousId = await AddPractitionerAsync("Previous", "Holder", applicationUserId: "user-1");
        var newId = await AddPractitionerAsync("New", "Holder");

        await _repository.SetLinkedUserAsync(newId, "user-1");

        var previous = await _context.Practitioners.FindAsync(previousId);
        var current = await _context.Practitioners.FindAsync(newId);

        previous!.ApplicationUserId.Should().BeNull("the previous holder is unlinked");
        current!.ApplicationUserId.Should().Be("user-1");
    }

    [Fact]
    public async Task SetLinkedUserAsync_WithNull_ClearsTheLink()
    {
        var id = await AddPractitionerAsync("Target", "Doctor", applicationUserId: "user-1");

        await _repository.SetLinkedUserAsync(id, null);

        var cleared = await _context.Practitioners.FindAsync(id);
        cleared!.ApplicationUserId.Should().BeNull();
    }

    [Fact]
    public async Task SetLinkedUserAsync_WithUnknownPractitioner_Throws()
    {
        var act = async () => await _repository.SetLinkedUserAsync(9999, "user-1");

        await act.Should().ThrowAsync<InvalidOperationException>();
    }

    [Fact]
    public async Task SearchAsync_MatchesOnLastName()
    {
        await AddPractitionerAsync("Jane", "Kowalski");
        await AddPractitionerAsync("John", "Smith");

        var results = await _repository.SearchAsync("kowal");

        results.Should().ContainSingle().Which.LastName.Should().Be("Kowalski");
    }
}
