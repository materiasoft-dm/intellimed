using FluentAssertions;
using IntelliMed.Core.DTOs;
using IntelliMed.Core.Entities;
using IntelliMed.Infrastructure.Data;
using IntelliMed.Infrastructure.Repositories;
using IntelliMed.Tests.Helpers;
using Xunit;

namespace IntelliMed.Tests.Repositories;

public class ClientHealthIssueRepositoryTests : IDisposable
{
    private readonly ClientHealthIssueRepository _repository;
    private readonly AppDbContext _context;
    private readonly int _clientId;

    public ClientHealthIssueRepositoryTests()
    {
        _context = TestDbContextFactory.CreateInMemoryContext();
        _repository = new ClientHealthIssueRepository(_context);

        var client = new Client { FirstName = "Health", LastName = "Issue" };
        _context.Clients.Add(client);
        _context.SaveChanges();
        _clientId = client.Id;
    }

    public void Dispose() => _context.Dispose();

    private CreateClientHealthIssueDto BaseDto(string condition = "Type 2 diabetes") => new()
    {
        ClientId = _clientId,
        Condition = condition,
        IsActive = true
    };

    [Fact]
    public async Task CreateAsync_PersistsAllFields()
    {
        var dto = BaseDto();
        dto.Code = "T90";
        dto.CodingSystem = "ICPC-2";
        dto.Side = BodySide.Left;
        dto.OnsetYear = 2015;
        dto.OnsetMonth = 3;
        dto.IsSummary = true;
        dto.IsConfidential = true;
        dto.Note = "Diet controlled";

        var id = await _repository.CreateAsync(dto);

        var created = await _context.ClientHealthIssues.FindAsync(id);
        created.Should().NotBeNull();
        created!.Condition.Should().Be("Type 2 diabetes");
        created.Code.Should().Be("T90");
        created.CodingSystem.Should().Be("ICPC-2");
        created.Side.Should().Be(BodySide.Left);
        created.OnsetYear.Should().Be(2015);
        created.OnsetMonth.Should().Be(3);
        created.IsActive.Should().BeTrue();
        created.IsSummary.Should().BeTrue();
        created.IsConfidential.Should().BeTrue();
        created.Note.Should().Be("Diet controlled");
        created.IsArchived.Should().BeFalse();
    }

    [Fact]
    public async Task CreateAsync_AllowsOnsetYearWithoutMonth()
    {
        var dto = BaseDto();
        dto.OnsetYear = 2015;
        dto.OnsetMonth = null;

        var id = await _repository.CreateAsync(dto);

        var created = await _context.ClientHealthIssues.FindAsync(id);
        created!.OnsetYear.Should().Be(2015);
        created.OnsetMonth.Should().BeNull();
    }

    [Fact]
    public async Task UpdateAsync_RoundTripsEveryEditableField()
    {
        var id = await _repository.CreateAsync(BaseDto());

        await _repository.UpdateAsync(id, new UpdateClientHealthIssueDto
        {
            Condition = "Type 2 diabetes mellitus",
            Side = BodySide.Bilateral,
            OnsetYear = 2011,
            OnsetMonth = 7,
            IsActive = false,
            IsSummary = true,
            IsConfidential = true,
            Note = "Now insulin dependent",
            IsArchived = false
        });

        var updated = await _context.ClientHealthIssues.FindAsync(id);
        updated!.Condition.Should().Be("Type 2 diabetes mellitus");
        updated.Side.Should().Be(BodySide.Bilateral);
        updated.OnsetYear.Should().Be(2011);
        updated.IsActive.Should().BeFalse();
        updated.IsSummary.Should().BeTrue();
        updated.UpdatedAt.Should().NotBeNull();
    }

    [Fact]
    public async Task UpdateAsync_WithUnknownId_Throws()
    {
        var act = async () => await _repository.UpdateAsync(9999, new UpdateClientHealthIssueDto { Condition = "Ghost" });

        await act.Should().ThrowAsync<InvalidOperationException>();
    }

    [Fact]
    public async Task GetByIdAsync_ReturnsNullForUnknownId()
    {
        var result = await _repository.GetByIdAsync(9999);

        result.Should().BeNull();
    }

    [Fact]
    public async Task GetByClientIdAsync_ExcludesArchivedByDefault()
    {
        var keptId = await _repository.CreateAsync(BaseDto("Asthma"));
        var archivedId = await _repository.CreateAsync(BaseDto("Entered in error"));
        await _repository.ArchiveAsync(archivedId);

        var result = (await _repository.GetByClientIdAsync(_clientId)).ToList();

        result.Should().ContainSingle().Which.Id.Should().Be(keptId);
    }

    [Fact]
    public async Task GetByClientIdAsync_IncludeArchivedReturnsAll()
    {
        await _repository.CreateAsync(BaseDto("Asthma"));
        var archivedId = await _repository.CreateAsync(BaseDto("Entered in error"));
        await _repository.ArchiveAsync(archivedId);

        var result = (await _repository.GetByClientIdAsync(_clientId, includeArchived: true)).ToList();

        result.Should().HaveCount(2);
    }

    [Fact]
    public async Task GetByClientIdAsync_OrdersActiveFirst()
    {
        var inactiveDto = BaseDto("Resolved condition");
        inactiveDto.IsActive = false;
        await _repository.CreateAsync(inactiveDto);
        await _repository.CreateAsync(BaseDto("Current condition"));

        var result = (await _repository.GetByClientIdAsync(_clientId)).ToList();

        result[0].Condition.Should().Be("Current condition");
    }

    [Fact]
    public async Task ArchiveAsync_SoftDeletesRatherThanRemovingTheRow()
    {
        var id = await _repository.CreateAsync(BaseDto());

        await _repository.ArchiveAsync(id);

        var stillThere = await _context.ClientHealthIssues.FindAsync(id);
        stillThere.Should().NotBeNull("a clinical record is archived, never erased");
        stillThere!.IsArchived.Should().BeTrue();
    }

    [Fact]
    public async Task ArchiveAsync_WithUnknownId_Throws()
    {
        var act = async () => await _repository.ArchiveAsync(9999);

        await act.Should().ThrowAsync<InvalidOperationException>();
    }

    [Fact]
    public async Task GetSummaryForClientAsync_ReturnsOnlyActiveUnarchivedSummaryFlagged()
    {
        var summaryDto = BaseDto("Shown on summary");
        summaryDto.IsSummary = true;
        await _repository.CreateAsync(summaryDto);

        // Summary-flagged but inactive — excluded.
        var inactiveSummary = BaseDto("Inactive summary");
        inactiveSummary.IsSummary = true;
        inactiveSummary.IsActive = false;
        await _repository.CreateAsync(inactiveSummary);

        // Summary-flagged but archived — excluded.
        var archivedSummary = BaseDto("Archived summary");
        archivedSummary.IsSummary = true;
        var archivedId = await _repository.CreateAsync(archivedSummary);
        await _repository.ArchiveAsync(archivedId);

        // Active but not flagged — excluded.
        await _repository.CreateAsync(BaseDto("Not flagged"));

        var result = (await _repository.GetSummaryForClientAsync(_clientId)).ToList();

        result.Should().ContainSingle().Which.Condition.Should().Be("Shown on summary");
    }

    [Fact]
    public async Task GetByClientIdAsync_IsScopedToTheRequestedClient()
    {
        var other = new Client { FirstName = "Other", LastName = "Patient" };
        _context.Clients.Add(other);
        await _context.SaveChangesAsync();

        await _repository.CreateAsync(BaseDto("Mine"));
        await _repository.CreateAsync(new CreateClientHealthIssueDto { ClientId = other.Id, Condition = "Theirs", IsActive = true });

        var result = (await _repository.GetByClientIdAsync(_clientId)).ToList();

        result.Should().ContainSingle().Which.Condition.Should().Be("Mine");
    }

    [Theory]
    [InlineData(2015, 3, "Mar 2015")]
    [InlineData(2015, null, "2015")]
    [InlineData(null, null, "")]
    [InlineData(2015, 13, "2015")] // out-of-range month falls back to year alone
    public async Task OnsetDisplay_FormatsTheImpreciseOnset(int? year, int? month, string expected)
    {
        var dto = BaseDto();
        dto.OnsetYear = year;
        dto.OnsetMonth = month;
        var id = await _repository.CreateAsync(dto);

        var result = await _repository.GetByIdAsync(id);

        result!.OnsetDisplay.Should().Be(expected);
    }
}
