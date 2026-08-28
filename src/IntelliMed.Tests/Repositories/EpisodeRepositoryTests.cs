using FluentAssertions;
using IntelliMed.Core.DTOs;
using IntelliMed.Core.Entities;
using IntelliMed.Infrastructure.Data;
using IntelliMed.Infrastructure.Repositories;
using IntelliMed.Tests.Helpers;
using Xunit;

namespace IntelliMed.Tests.Repositories;

public class EpisodeRepositoryTests : IDisposable
{
    private readonly EpisodeRepository _repository;
    private readonly AppDbContext _context;
    private readonly int _clientId;

    public EpisodeRepositoryTests()
    {
        _context = TestDbContextFactory.CreateInMemoryContext();
        _repository = new EpisodeRepository(_context);

        var client = new Client { FirstName = "Episode", LastName = "Test" };
        _context.Clients.Add(client);
        _context.SaveChanges();
        _clientId = client.Id;
    }

    public void Dispose() => _context.Dispose();

    [Fact]
    public async Task CreateAsync_SetsStatusActiveAndOpenedDate()
    {
        var id = await _repository.CreateAsync(new CreateEpisodeDto { ClientId = _clientId, Title = "Right knee injury" });

        var created = await _context.Episodes.FindAsync(id);
        created!.Status.Should().Be(EpisodeStatus.Active);
        created.OpenedDate.Should().Be(DateTime.UtcNow.Date);
        created.IsArchived.Should().BeFalse();
    }

    [Fact]
    public async Task UpdateAsync_RenamesTitleAndDescription()
    {
        var id = await _repository.CreateAsync(new CreateEpisodeDto { ClientId = _clientId, Title = "Knee injury" });

        await _repository.UpdateAsync(id, new UpdateEpisodeDto { Title = "Right knee injury", Description = "Post football injury" });

        var updated = await _context.Episodes.FindAsync(id);
        updated!.Title.Should().Be("Right knee injury");
        updated.Description.Should().Be("Post football injury");
    }

    [Fact]
    public async Task ResolveAsync_SetsStatusResolvedAndClosedDate()
    {
        var id = await _repository.CreateAsync(new CreateEpisodeDto { ClientId = _clientId, Title = "Knee injury" });

        await _repository.ResolveAsync(id);

        var resolved = await _context.Episodes.FindAsync(id);
        resolved!.Status.Should().Be(EpisodeStatus.Resolved);
        resolved.ClosedDate.Should().NotBeNull();
    }

    [Fact]
    public async Task ReopenAsync_ClearsClosedDateAndSetsActive()
    {
        var id = await _repository.CreateAsync(new CreateEpisodeDto { ClientId = _clientId, Title = "Knee injury" });
        await _repository.ResolveAsync(id);

        await _repository.ReopenAsync(id);

        var reopened = await _context.Episodes.FindAsync(id);
        reopened!.Status.Should().Be(EpisodeStatus.Active);
        reopened.ClosedDate.Should().BeNull();
    }

    [Fact]
    public async Task ArchiveAsync_SetsIsArchivedTrue()
    {
        var id = await _repository.CreateAsync(new CreateEpisodeDto { ClientId = _clientId, Title = "Knee injury" });

        await _repository.ArchiveAsync(id);

        (await _context.Episodes.FindAsync(id))!.IsArchived.Should().BeTrue();
    }

    [Fact]
    public async Task GetByClientIdAsync_DefaultsToExcludingArchived()
    {
        var visibleId = await _repository.CreateAsync(new CreateEpisodeDto { ClientId = _clientId, Title = "Visible" });
        var archivedId = await _repository.CreateAsync(new CreateEpisodeDto { ClientId = _clientId, Title = "Archived" });
        await _repository.ArchiveAsync(archivedId);

        var result = (await _repository.GetByClientIdAsync(_clientId)).ToList();

        result.Should().ContainSingle();
        result[0].Id.Should().Be(visibleId);
    }

    [Fact]
    public async Task GetByClientIdAsync_IncludeArchivedReturnsAll()
    {
        await _repository.CreateAsync(new CreateEpisodeDto { ClientId = _clientId, Title = "Visible" });
        var archivedId = await _repository.CreateAsync(new CreateEpisodeDto { ClientId = _clientId, Title = "Archived" });
        await _repository.ArchiveAsync(archivedId);

        var result = (await _repository.GetByClientIdAsync(_clientId, includeArchived: true)).ToList();

        result.Should().HaveCount(2);
    }
}
