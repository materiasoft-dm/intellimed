using FluentAssertions;
using IntelliMed.Core.Entities;
using IntelliMed.Infrastructure.Data;
using IntelliMed.Infrastructure.Repositories;
using IntelliMed.Infrastructure.Services;
using IntelliMed.Tests.Helpers;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace IntelliMed.Tests.Repositories;

public class ResultRepositoryTests : IDisposable
{
    private const int ClinicId = 1;
    private const int OtherClinicId = 2;

    private readonly AppDbContext _context;
    private readonly ResultIngestService _ingest;
    private readonly ResultRepository _repository;
    private readonly int _janeId;
    private readonly int _drChenId;

    public ResultRepositoryTests()
    {
        _context = TestDbContextFactory.CreateInMemoryContext();
        _ingest = new ResultIngestService(_context);
        _repository = new ResultRepository(_context);

        // Clinic 1 ("Main Clinic") is seeded by the model; only the second needs adding.
        _context.Clinics.Add(new Clinic { Id = OtherClinicId, Name = "Other" });

        var jane = new Client
        {
            ClinicId = ClinicId, FirstName = "Jane", LastName = "Citizen",
            DateOfBirth = new DateTime(1980, 3, 15), MedicareNumber = "2123456701"
        };
        var chen = new Practitioner { FirstName = "Sarah", LastName = "Chen", Title = "Dr", ProviderNumber = "2345678A" };
        _context.Clients.Add(jane);
        _context.Practitioners.Add(chen);
        _context.SaveChanges();
        _janeId = jane.Id;
        _drChenId = chen.Id;
    }

    public void Dispose() => _context.Dispose();

    private Client AddClient(string first, string last, DateTime dob, string medicare = "", int clinic = ClinicId)
    {
        var c = new Client { ClinicId = clinic, FirstName = first, LastName = last, DateOfBirth = dob, MedicareNumber = medicare };
        _context.Clients.Add(c);
        _context.SaveChanges();
        return c;
    }

    // ---- ingest & matching ----

    [Fact]
    public async Task Ingest_ExactMedicareAndDob_FilesUnderTheClientInTheInbox()
    {
        var outcome = await _ingest.IngestAsync(Hl7Fixtures.FullBloodCount(), ClinicId);

        outcome.Duplicate.Should().BeFalse();
        outcome.Status.Should().Be(ResultStatus.Inbox);
        var report = await _context.ResultReports.Include(r => r.Items).SingleAsync();
        report.ClientId.Should().Be(_janeId);
        report.HasAbnormal.Should().BeTrue();
        report.Items.Should().HaveCount(2);
        report.RawMessage.Should().Contain("MSG0001");
    }

    [Fact]
    public async Task Ingest_RoutesToTheOrderingDoctorByProviderNumber()
    {
        await _ingest.IngestAsync(Hl7Fixtures.FullBloodCount(), ClinicId);

        (await _context.ResultReports.SingleAsync()).AssignedPractitionerId.Should().Be(_drChenId);
    }

    [Fact]
    public async Task Ingest_UnknownOrderingDoctor_LeavesItInTheClinicWideInbox()
    {
        await _ingest.IngestAsync(Hl7Fixtures.FullBloodCount(orderingNumber: "9999999Z", orderingFamily: "Nobody", orderingGiven: "Known"), ClinicId);

        (await _context.ResultReports.SingleAsync()).AssignedPractitionerId.Should().BeNull();
    }

    [Fact]
    public async Task Ingest_NameAndDobMatchWithNoMedicare_Matches()
    {
        await _ingest.IngestAsync(Hl7Fixtures.FullBloodCount(medicare: "", labMrn: "X1"), ClinicId);

        (await _context.ResultReports.SingleAsync()).ClientId.Should().Be(_janeId);
    }

    [Fact]
    public async Task Ingest_TwoClientsWithSameNameAndDob_IsLeftUnmatched()
    {
        AddClient("Jane", "Citizen", new DateTime(1980, 3, 15));

        var outcome = await _ingest.IngestAsync(Hl7Fixtures.FullBloodCount(medicare: "", labMrn: "X2"), ClinicId);

        outcome.Status.Should().Be(ResultStatus.Unmatched);
        (await _context.ResultReports.SingleAsync()).ClientId.Should().BeNull();
    }

    [Fact]
    public async Task Ingest_SameNameDifferentDob_IsNotMatched()
    {
        var outcome = await _ingest.IngestAsync(Hl7Fixtures.FullBloodCount(dob: "19900101", medicare: "", labMrn: "X3"), ClinicId);

        outcome.Status.Should().Be(ResultStatus.Unmatched);
    }

    [Fact]
    public async Task Ingest_MedicareWithoutDob_IsNotMatched()
    {
        var message = Hl7Fixtures.FullBloodCount().Replace("||19800315|F", "|||F");

        var outcome = await _ingest.IngestAsync(message, ClinicId);

        outcome.Status.Should().Be(ResultStatus.Unmatched, "a Medicare number alone is not safe to file on");
    }

    [Fact]
    public async Task Ingest_ClientInAnotherClinic_IsNotMatched()
    {
        var outcome = await _ingest.IngestAsync(Hl7Fixtures.FullBloodCount(), OtherClinicId);

        outcome.Status.Should().Be(ResultStatus.Unmatched);
    }

    [Fact]
    public async Task Ingest_SameMessageTwice_IsIgnoredTheSecondTime()
    {
        var first = await _ingest.IngestAsync(Hl7Fixtures.FullBloodCount(), ClinicId);
        var second = await _ingest.IngestAsync(Hl7Fixtures.FullBloodCount(), ClinicId);

        second.Duplicate.Should().BeTrue();
        second.ReportId.Should().Be(first.ReportId);
        (await _context.ResultReports.CountAsync()).Should().Be(1);
    }

    [Fact]
    public async Task Ingest_SameControlIdFromDifferentLab_IsAnotherReport()
    {
        await _ingest.IngestAsync(Hl7Fixtures.FullBloodCount(), ClinicId);
        await _ingest.IngestAsync(Hl7Fixtures.FullBloodCount(sender: "OTHER LAB"), ClinicId);

        (await _context.ResultReports.CountAsync()).Should().Be(2);
    }

    [Fact]
    public async Task Ingest_UnreadableMessage_ThrowsArgumentException()
    {
        var act = () => _ingest.IngestAsync("garbage", ClinicId);

        await act.Should().ThrowAsync<ArgumentException>();
    }

    // ---- review ----

    private async Task<int> IngestUnmatchedAsync()
    {
        var outcome = await _ingest.IngestAsync(
            Hl7Fixtures.FullBloodCount(controlId: "U1", family: "Smythe", given: "Jon", dob: "19700101", medicare: "", labMrn: "LAB-U1"), ClinicId);
        outcome.Status.Should().Be(ResultStatus.Unmatched);
        return outcome.ReportId;
    }

    [Fact]
    public async Task Match_MovesAnUnmatchedReportToTheInbox_AndRemembersTheLabNumber()
    {
        var id = await IngestUnmatchedAsync();
        var jon = AddClient("Jonathan", "Smith", new DateTime(1970, 1, 1));

        (await _repository.MatchAsync(id, jon.Id, true, "user-1")).Should().BeTrue();

        var report = await _context.ResultReports.SingleAsync(r => r.Id == id);
        report.Status.Should().Be(ResultStatus.Inbox);
        report.ClientId.Should().Be(jon.Id);
        report.MatchedByUserId.Should().Be("user-1");
        (await _context.ClientLabIdentifiers.SingleAsync()).Identifier.Should().Be("LAB-U1");

        // The lab's next report for that patient number now files itself.
        var next = await _ingest.IngestAsync(
            Hl7Fixtures.FullBloodCount(controlId: "U2", family: "Smythe", given: "Jon", dob: "19700101", medicare: "", labMrn: "LAB-U1"), ClinicId);
        next.Status.Should().Be(ResultStatus.Inbox);
    }

    [Fact]
    public async Task Match_ClientFromAnotherClinic_IsRefused()
    {
        var id = await IngestUnmatchedAsync();
        var elsewhere = AddClient("Other", "Person", new DateTime(1970, 1, 1), clinic: OtherClinicId);

        (await _repository.MatchAsync(id, elsewhere.Id, true, "user-1")).Should().BeFalse();

        (await _context.ResultReports.SingleAsync(r => r.Id == id)).Status.Should().Be(ResultStatus.Unmatched);
    }

    [Fact]
    public async Task Acknowledge_ArchivesAndRecordsWhoAndWhen()
    {
        var id = (await _ingest.IngestAsync(Hl7Fixtures.FullBloodCount(), ClinicId)).ReportId;

        (await _repository.AcknowledgeAsync(id, "  Repeat in 3 months  ", "doc-1")).Should().BeTrue();

        var report = await _context.ResultReports.SingleAsync();
        report.Status.Should().Be(ResultStatus.Archived);
        report.ReviewedByUserId.Should().Be("doc-1");
        report.ReviewedAt.Should().NotBeNull();
        report.ReviewComment.Should().Be("Repeat in 3 months");
    }

    [Fact]
    public async Task Acknowledge_UnmatchedReport_IsRefused()
    {
        var id = await IngestUnmatchedAsync();

        (await _repository.AcknowledgeAsync(id, null, "doc-1")).Should().BeFalse();

        (await _context.ResultReports.SingleAsync()).Status.Should().Be(ResultStatus.Unmatched);
    }

    [Fact]
    public async Task HoldAndReopen_RoundTripThroughHeld()
    {
        var id = (await _ingest.IngestAsync(Hl7Fixtures.FullBloodCount(), ClinicId)).ReportId;

        (await _repository.HoldAsync(id, "Waiting on iron studies")).Should().BeTrue();
        (await _context.ResultReports.SingleAsync()).Status.Should().Be(ResultStatus.Held);

        (await _repository.ReopenAsync(id)).Should().BeTrue();
        var report = await _context.ResultReports.SingleAsync();
        report.Status.Should().Be(ResultStatus.Inbox);
        report.HoldNote.Should().BeNull();
    }

    // ---- searching & counts ----

    [Fact]
    public async Task Search_MineIncludesOwnAndClinicWide_ButNotAnotherDoctors()
    {
        var other = new Practitioner { FirstName = "Bob", LastName = "Other", Title = "Dr", ProviderNumber = "1111111B" };
        _context.Practitioners.Add(other);
        await _context.SaveChangesAsync();

        await _ingest.IngestAsync(Hl7Fixtures.FullBloodCount(controlId: "A"), ClinicId);                                   // Dr Chen
        await _ingest.IngestAsync(Hl7Fixtures.FullBloodCount(controlId: "B", orderingNumber: "1111111B", orderingFamily: "Other", orderingGiven: "Bob"), ClinicId); // Dr Other
        await _ingest.IngestAsync(Hl7Fixtures.FullBloodCount(controlId: "C", orderingNumber: "0", orderingFamily: "X", orderingGiven: "Y"), ClinicId);          // unassigned

        var mine = await _repository.SearchAsync(new Core.DTOs.ResultSearchDto { ClinicId = ClinicId, PractitionerId = _drChenId });
        var all = await _repository.SearchAsync(new Core.DTOs.ResultSearchDto { ClinicId = ClinicId });

        mine.TotalCount.Should().Be(2);
        all.TotalCount.Should().Be(3);
    }

    [Fact]
    public async Task Search_IsScopedToTheClinic()
    {
        await _ingest.IngestAsync(Hl7Fixtures.FullBloodCount(), ClinicId);

        var other = await _repository.SearchAsync(new Core.DTOs.ResultSearchDto { ClinicId = OtherClinicId });

        other.TotalCount.Should().Be(0);
    }

    [Fact]
    public async Task Counts_ExcludeArchivedAndSplitByStatus()
    {
        var a = (await _ingest.IngestAsync(Hl7Fixtures.FullBloodCount(controlId: "A"), ClinicId)).ReportId;
        await _ingest.IngestAsync(Hl7Fixtures.FullBloodCount(controlId: "B"), ClinicId);
        await IngestUnmatchedAsync();
        await _repository.AcknowledgeAsync(a, null, "doc-1");

        var counts = await _repository.GetCountsAsync(ClinicId, null);

        counts.Inbox.Should().Be(1);
        counts.InboxAbnormal.Should().Be(1);
        counts.Unmatched.Should().Be(1);
        counts.Held.Should().Be(0);
    }

    // ---- ingest keys ----

    [Fact]
    public async Task IngestKey_IsShownOnce_StoredHashed_AndResolvesToItsClinic()
    {
        var created = await _repository.CreateIngestKeyAsync(ClinicId, "Acme relay");

        created.Key.Should().StartWith("imr_");
        var stored = await _context.ResultIngestKeys.SingleAsync();
        stored.KeyHash.Should().NotContain(created.Key).And.HaveLength(64);

        (await _repository.ResolveIngestKeyAsync(created.Key)).Should().Be(ClinicId);
        (await _context.ResultIngestKeys.SingleAsync()).LastUsedAt.Should().NotBeNull();
        (await _repository.ResolveIngestKeyAsync(created.Key + "x")).Should().BeNull();
        (await _repository.ResolveIngestKeyAsync("")).Should().BeNull();
    }

    [Fact]
    public async Task IngestKey_RevokedKeyNoLongerResolves()
    {
        var created = await _repository.CreateIngestKeyAsync(ClinicId, "Acme relay");

        await _repository.RevokeIngestKeyAsync(created.Id);

        (await _repository.ResolveIngestKeyAsync(created.Key)).Should().BeNull();
    }
}
