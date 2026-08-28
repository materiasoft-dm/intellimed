using System.Text.RegularExpressions;
using IntelliMed.Core.Entities;
using IntelliMed.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Playwright;
using Xunit;

namespace IntelliMed.Tests.Ui;

// Covers the Health Issues tab on the client record (the problem list ported from Medinet's
// PastHistoryCtrl) plus its one cross-module touchpoint: summary-flagged issues surfacing on the
// progress-note editor.
[Collection("Playwright UI")]
public class HealthIssueTests : UiTestBase
{
    public HealthIssueTests(PlaywrightServerFixture fixture) : base(fixture) { }

    private static async Task<(IPage Page, string Id)> CreateClientAsync(PlaywrightServerFixture fixture, string surname, string given)
    {
        var page = await fixture.NewAuthenticatedPageAsync();
        await page.GotoAsync("/clients/add");
        await page.WaitForSelectorAsync("text=New Client");
        await page.Field("Surname").FillAsync(surname);
        await page.Field("Given").FillAsync(given);
        await page.GetByRole(AriaRole.Button, new() { Name = "💾 Save" }).ClickAsync();
        await page.WaitForURLAsync(url => url.Contains("/clients/edit/"));

        var match = Regex.Match(page.Url, @"/clients/edit/(\d+)");
        return (page, match.Groups[1].Value);
    }

    private AppDbContext OpenFixtureDb()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlite($"Data Source={Fixture.DbPath};Pooling=false")
            .Options;
        return new AppDbContext(options);
    }

    private async Task SeedPractitionerAsync(string firstName, string lastName)
    {
        await using var context = OpenFixtureDb();
        context.Practitioners.Add(new Practitioner { FirstName = firstName, LastName = lastName, Title = "Dr", IsActive = true });
        await context.SaveChangesAsync();
    }

    /// <summary>Opens the Health Issues tab on an existing client's record.</summary>
    private static async Task OpenTabAsync(IPage page, string clientId)
    {
        await page.GotoAsync($"/clients/edit/{clientId}");
        await page.WaitForSelectorAsync("text=Edit Client");
        await page.TabLink("Health Issues").ClickAsync();
    }

    /// <summary>Fills and saves the add-issue modal. Condition only, unless extras are supplied.</summary>
    private static async Task AddIssueAsync(IPage page, string condition, int? onsetYear = null, bool summary = false)
    {
        await page.GetByRole(AriaRole.Button, new() { Name = "+ New" }).ClickAsync();
        await page.WaitForSelectorAsync("text=New Health Issue");

        await page.Field("Condition").FillAsync(condition);
        if (onsetYear.HasValue)
            await page.Field("Onset year").FillAsync(onsetYear.Value.ToString());
        if (summary)
            await page.GetByLabel("Show on summary").CheckAsync();

        await page.GetByRole(AriaRole.Button, new() { Name = "Save", Exact = true }).ClickAsync();
        await page.WaitForSelectorAsync("text=New Health Issue", new PageWaitForSelectorOptions { State = WaitForSelectorState.Detached });
    }

    [Fact]
    public async Task HealthIssuesTab_Loads_WithFiltersAndEmptyState()
    {
        var (_, clientId) = await CreateClientAsync(Fixture, "HiEmpty", "Patient");
        var page = await Fixture.NewAuthenticatedPageAsync();

        await OpenTabAsync(page, clientId);

        await Assertions.Expect(page.GetByText("No health issues recorded.")).ToBeVisibleAsync();
        foreach (var filter in new[] { "All", "Active", "Inactive", "Summary" })
        {
            await Assertions.Expect(page.GetByRole(AriaRole.Button, new() { Name = filter, Exact = true })).ToBeVisibleAsync();
        }
    }

    [Fact]
    public async Task AddIssue_AppearsInTheList_WithOnsetYearOnly()
    {
        var (_, clientId) = await CreateClientAsync(Fixture, "HiAdd", "Patient");
        var page = await Fixture.NewAuthenticatedPageAsync();

        await OpenTabAsync(page, clientId);
        await AddIssueAsync(page, "Playwright Asthma", onsetYear: 2015);

        var row = page.Locator("tr", new PageLocatorOptions { HasTextString = "Playwright Asthma" });
        await Assertions.Expect(row).ToBeVisibleAsync();
        // Year-only onset renders as the bare year — no fabricated day/month.
        await Assertions.Expect(row).ToContainTextAsync("2015");
        await Assertions.Expect(row).ToContainTextAsync("Active");
    }

    [Fact]
    public async Task RequiresACondition_BeforeSaving()
    {
        var (_, clientId) = await CreateClientAsync(Fixture, "HiValidate", "Patient");
        var page = await Fixture.NewAuthenticatedPageAsync();

        await OpenTabAsync(page, clientId);
        await page.GetByRole(AriaRole.Button, new() { Name = "+ New" }).ClickAsync();
        await page.WaitForSelectorAsync("text=New Health Issue");
        await page.GetByRole(AriaRole.Button, new() { Name = "Save", Exact = true }).ClickAsync();

        await Assertions.Expect(page.GetByText("Condition is required.")).ToBeVisibleAsync();
    }

    [Fact]
    public async Task MarkingInactive_MovesItBetweenTheActiveAndInactiveFilters()
    {
        var (_, clientId) = await CreateClientAsync(Fixture, "HiInactive", "Patient");
        var page = await Fixture.NewAuthenticatedPageAsync();

        await OpenTabAsync(page, clientId);
        await AddIssueAsync(page, "Playwright Resolved Condition");

        // Reopen and untick Active.
        await page.GetByRole(AriaRole.Link, new() { Name = "Playwright Resolved Condition" }).ClickAsync();
        await page.WaitForSelectorAsync("text=Edit Health Issue");
        await page.GetByLabel("Active", new() { Exact = true }).UncheckAsync();
        await page.GetByRole(AriaRole.Button, new() { Name = "Save", Exact = true }).ClickAsync();
        await page.WaitForSelectorAsync("text=Edit Health Issue", new PageWaitForSelectorOptions { State = WaitForSelectorState.Detached });

        // Default filter is Active, so it should now be hidden...
        await Assertions.Expect(page.Locator("tr", new PageLocatorOptions { HasTextString = "Playwright Resolved Condition" }))
            .ToHaveCountAsync(0);

        // ...and visible under Inactive.
        await page.GetByRole(AriaRole.Button, new() { Name = "Inactive", Exact = true }).ClickAsync();
        await Assertions.Expect(page.Locator("tr", new PageLocatorOptions { HasTextString = "Playwright Resolved Condition" }))
            .ToBeVisibleAsync();
    }

    [Fact]
    public async Task Archiving_RemovesItFromEveryClinicalFilter()
    {
        var (_, clientId) = await CreateClientAsync(Fixture, "HiArchive", "Patient");
        var page = await Fixture.NewAuthenticatedPageAsync();

        await OpenTabAsync(page, clientId);
        await AddIssueAsync(page, "Playwright Entered In Error");

        var row = page.Locator("tr", new PageLocatorOptions { HasTextString = "Playwright Entered In Error" });
        await row.GetByRole(AriaRole.Button, new() { Name = "Archive" }).ClickAsync();

        await Assertions.Expect(page.Locator("tr", new PageLocatorOptions { HasTextString = "Playwright Entered In Error" }))
            .ToHaveCountAsync(0);

        await page.GetByRole(AriaRole.Button, new() { Name = "Inactive", Exact = true }).ClickAsync();
        await Assertions.Expect(page.Locator("tr", new PageLocatorOptions { HasTextString = "Playwright Entered In Error" }))
            .ToHaveCountAsync(0);
    }

    [Fact]
    public async Task SummaryFilter_ShowsOnlyFlaggedIssues()
    {
        var (_, clientId) = await CreateClientAsync(Fixture, "HiSummaryFilter", "Patient");
        var page = await Fixture.NewAuthenticatedPageAsync();

        await OpenTabAsync(page, clientId);
        await AddIssueAsync(page, "Playwright Flagged Issue", summary: true);
        await AddIssueAsync(page, "Playwright Unflagged Issue");

        await page.GetByRole(AriaRole.Button, new() { Name = "Summary", Exact = true }).ClickAsync();

        await Assertions.Expect(page.Locator("tr", new PageLocatorOptions { HasTextString = "Playwright Flagged Issue" }))
            .ToBeVisibleAsync();
        await Assertions.Expect(page.Locator("tr", new PageLocatorOptions { HasTextString = "Playwright Unflagged Issue" }))
            .ToHaveCountAsync(0);
    }

    [Fact]
    public async Task SummaryFlaggedIssues_RenderOnTheProgressNoteEditor()
    {
        await SeedPractitionerAsync("Hank", "HealthIssue1");
        var (_, clientId) = await CreateClientAsync(Fixture, "HiOnNote", "Patient");
        var page = await Fixture.NewAuthenticatedPageAsync();

        await OpenTabAsync(page, clientId);
        await AddIssueAsync(page, "Playwright Chronic Kidney Disease", onsetYear: 2019, summary: true);
        await AddIssueAsync(page, "Playwright Not On Summary");

        await page.GotoAsync($"/clients/{clientId}/progress-notes/new");
        await page.WaitForSelectorAsync("text=Reason for Visit");

        var banner = page.Locator(".alert", new PageLocatorOptions { HasTextString = "Health issues:" });
        await Assertions.Expect(banner).ToBeVisibleAsync();
        await Assertions.Expect(banner).ToContainTextAsync("Playwright Chronic Kidney Disease");
        await Assertions.Expect(banner).ToContainTextAsync("2019");
        await Assertions.Expect(banner).Not.ToContainTextAsync("Playwright Not On Summary");
    }
}
