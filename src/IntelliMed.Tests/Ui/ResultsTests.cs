using IntelliMed.Core.Entities;
using IntelliMed.Tests.Helpers;
using Microsoft.EntityFrameworkCore;
using Microsoft.Playwright;
using System.Text;
using Xunit;

namespace IntelliMed.Tests.Ui;

// Covers the results inbox: uploading an HL7 report, auto-filing under a patient, manual matching of an
// unmatched report, acknowledging, holding, and the ingest endpoint with its API key.
[Collection("Playwright UI")]
public class ResultsTests : UiTestBase
{
    private static readonly DateTime Dob = new(1975, 6, 20);

    public ResultsTests(PlaywrightServerFixture fixture) : base(fixture) { }

    private async Task<int> SeedClientAsync(string surname, string given = "Jane")
    {
        await using var context = Fixture.OpenDb();
        var client = new Client { ClinicId = 1, FirstName = given, LastName = surname, DateOfBirth = Dob, MedicareNumber = "" };
        context.Clients.Add(client);
        await context.SaveChangesAsync();
        return client.Id;
    }

    /// <summary>An FBC report for the given patient, from a lab name unique to the test so runs never collide.</summary>
    private static (string Message, string Lab) Report(string surname, string given = "Jane")
    {
        var lab = UiSeedHelpers.Unique("PW Lab ");
        var message = Hl7Fixtures.FullBloodCount(
            controlId: UiSeedHelpers.Unique("MSG"), family: surname, given: given,
            dob: Dob.ToString("yyyyMMdd"), medicare: "", labMrn: UiSeedHelpers.Unique("MRN"), sender: lab);
        return (message, lab);
    }

    private static async Task UploadAsync(IPage page, string message)
    {
        await page.Locator("#uploadResult").SetInputFilesAsync(new FilePayload
        {
            Name = "report.hl7",
            MimeType = "text/plain",
            Buffer = Encoding.UTF8.GetBytes(message)
        });
    }

    private static ILocator Row(IPage page, string text) => page.Locator("tr.result-row", new() { HasTextString = text });

    private async Task<IPage> OpenResultsAsync()
    {
        var page = await Fixture.NewAuthenticatedPageAsync();
        await page.GotoAsync("/results");
        await Assertions.Expect(page.GetByRole(AriaRole.Heading, new() { Name = "Results" })).ToBeVisibleAsync();
        // "My results and unassigned" would hide reports routed to another practitioner; the seeded admin
        // has no practitioner link, so it already sees everything, but be explicit.
        return page;
    }

    [Fact]
    public async Task Sidebar_ResultsLink_OpensTheResultsPage()
    {
        var page = await Fixture.NewAuthenticatedPageAsync();
        await page.GotoAsync("/");
        await page.WaitForSelectorAsync("text=Welcome,");

        await page.Locator("a.mdi-nav-link[href='/results']").ClickAsync();

        await page.WaitForURLAsync(url => url.EndsWith("/results"));
        await Assertions.Expect(page.GetByRole(AriaRole.Heading, new() { Name = "Results" })).ToBeVisibleAsync();
    }

    [Fact]
    public async Task Upload_ReportForAKnownPatient_IsFiledInTheInbox_AndCanBeAcknowledged()
    {
        var surname = UiSeedHelpers.Unique("Matchable");
        var clientId = await SeedClientAsync(surname);
        var (message, _) = Report(surname);

        var page = await OpenResultsAsync();
        await UploadAsync(page, message);

        await Assertions.Expect(page.GetByText("Report received and filed in the inbox.")).ToBeVisibleAsync();
        var row = Row(page, surname);
        await Assertions.Expect(row).ToContainTextAsync("Full Blood Count");
        await Assertions.Expect(row.Locator("i.bi-exclamation-triangle-fill")).ToBeVisibleAsync();

        await row.ClickAsync();
        var items = page.Locator("#resultItems");
        await Assertions.Expect(items).ToContainTextAsync("Haemoglobin");
        await Assertions.Expect(items.Locator("tr.table-danger")).ToHaveCountAsync(1);

        await page.Locator("#reviewComment").FillAsync("Repeat in 3 months");
        await page.Locator("#btnAcknowledge").ClickAsync();

        await Assertions.Expect(page.GetByText("Report acknowledged.")).ToBeVisibleAsync();
        await Assertions.Expect(Row(page, surname)).ToHaveCountAsync(0);

        await page.Locator("#tab-archived").ClickAsync();
        await Assertions.Expect(Row(page, surname)).ToBeVisibleAsync();

        await using var db = Fixture.OpenDb();
        var report = await db.ResultReports.SingleAsync(r => r.ClientId == clientId);
        Assert.Equal(ResultStatus.Archived, report.Status);
        Assert.Equal("Repeat in 3 months", report.ReviewComment);
        Assert.NotNull(report.ReviewedByUserId);
    }

    [Fact]
    public async Task Upload_ReportForAnUnknownPatient_WaitsInUnmatched_UntilMatchedByHand()
    {
        var lookup = UiSeedHelpers.Unique("Hand");
        var clientId = await SeedClientAsync(lookup, "Pat");
        // The lab spells the surname differently, so it cannot match automatically.
        var labSurname = UiSeedHelpers.Unique("Mis");
        var (message, _) = Report(labSurname, given: "Patricia");

        var page = await OpenResultsAsync();
        await UploadAsync(page, message);
        await Assertions.Expect(page.GetByText("couldn't be matched to a patient")).ToBeVisibleAsync();

        await page.Locator("#tab-unmatched").ClickAsync();
        var row = Row(page, labSurname);
        await Assertions.Expect(row).ToContainTextAsync("Needs matching");
        await row.ClickAsync();

        await page.Locator("#btnFindPatient").ClickAsync();
        var modal = page.Locator(".modal-content", new() { HasTextString = "Match to patient" });
        await modal.Locator("xpath=.//label[normalize-space(text())='Surname']/following-sibling::input").FillAsync(lookup);
        await modal.GetByRole(AriaRole.Button, new() { Name = "Search" }).ClickAsync();
        await modal.Locator("tr", new() { HasTextString = lookup }).ClickAsync();

        await Assertions.Expect(page.GetByText("File this report under this patient?")).ToBeVisibleAsync();
        await page.Locator("#btnConfirmMatch").ClickAsync();

        await Assertions.Expect(page.GetByText("Report filed under the patient and moved to the inbox.")).ToBeVisibleAsync();

        await using var db = Fixture.OpenDb();
        var report = await db.ResultReports.SingleAsync(r => r.PatientFamilyName == labSurname);
        Assert.Equal(ResultStatus.Inbox, report.Status);
        Assert.Equal(clientId, report.ClientId);
        Assert.True(await db.ClientLabIdentifiers.AnyAsync(l => l.ClientId == clientId));
    }

    [Fact]
    public async Task Upload_SameReportTwice_IsNotAddedTwice()
    {
        var surname = UiSeedHelpers.Unique("Twice");
        await SeedClientAsync(surname);
        var (message, _) = Report(surname);

        var page = await OpenResultsAsync();
        await UploadAsync(page, message);
        await Assertions.Expect(page.GetByText("Report received and filed in the inbox.")).ToBeVisibleAsync();
        await UploadAsync(page, message);

        await Assertions.Expect(page.GetByText("That report has already been received")).ToBeVisibleAsync();
        await Assertions.Expect(Row(page, surname)).ToHaveCountAsync(1);
    }

    [Fact]
    public async Task Upload_FileThatIsNotHl7_ShowsAnError()
    {
        var page = await OpenResultsAsync();

        await UploadAsync(page, "this is not an HL7 message");

        await Assertions.Expect(page.Locator(".alert-danger")).ToContainTextAsync("HL7");
    }

    [Fact]
    public async Task Hold_MovesAReportToHeld_AndReopenReturnsItToTheInbox()
    {
        var surname = UiSeedHelpers.Unique("Holdable");
        await SeedClientAsync(surname);
        var (message, _) = Report(surname);

        var page = await OpenResultsAsync();
        await UploadAsync(page, message);
        await Row(page, surname).ClickAsync();
        await page.Locator("#reviewComment").FillAsync("Waiting on iron studies");
        await page.Locator("#btnHold").ClickAsync();

        await Assertions.Expect(page.GetByText("Report held for later.")).ToBeVisibleAsync();
        await page.Locator("#tab-held").ClickAsync();
        await Row(page, surname).ClickAsync();
        await Assertions.Expect(page.GetByText("Held: Waiting on iron studies")).ToBeVisibleAsync();

        await page.Locator("#btnReopen").ClickAsync();

        await Assertions.Expect(page.GetByText("Report moved back to the inbox.")).ToBeVisibleAsync();
        await page.Locator("#tab-inbox").ClickAsync();
        await Assertions.Expect(Row(page, surname)).ToBeVisibleAsync();
    }

    [Fact]
    public async Task IngestEndpoint_AcceptsAReportWithAKey_AndRejectsAMissingOrWrongOne()
    {
        var surname = UiSeedHelpers.Unique("Feed");
        await SeedClientAsync(surname);
        var (message, _) = Report(surname);

        var page = await OpenResultsAsync();
        await page.Locator("#btnConnections").ClickAsync();
        await page.Locator("#keyLabel").FillAsync("Playwright relay");
        await page.Locator("#btnCreateKey").ClickAsync();
        await Assertions.Expect(page.Locator("#newKeyAlert")).ToBeVisibleAsync();
        var key = (await page.Locator("#newKeyValue").InnerTextAsync()).Trim();

        var api = await Fixture.PlaywrightInstance.APIRequest.NewContextAsync(new APIRequestNewContextOptions { BaseURL = Fixture.BaseUrl });

        var noKey = await api.PostAsync("/api/results/ingest", new() { Data = message });
        Assert.Equal(401, noKey.Status);

        var wrongKey = await api.PostAsync("/api/results/ingest", new() { Data = message, Headers = new Dictionary<string, string> { ["X-Api-Key"] = key + "x" } });
        Assert.Equal(401, wrongKey.Status);

        var ok = await api.PostAsync("/api/results/ingest", new() { Data = message, Headers = new Dictionary<string, string> { ["X-Api-Key"] = key } });
        Assert.Equal(200, ok.Status);

        var notHl7 = await api.PostAsync("/api/results/ingest", new() { Data = "nope", Headers = new Dictionary<string, string> { ["X-Api-Key"] = key } });
        Assert.Equal(400, notHl7.Status);

        await page.ReloadAsync();
        await Assertions.Expect(Row(page, surname)).ToBeVisibleAsync();
    }
}
