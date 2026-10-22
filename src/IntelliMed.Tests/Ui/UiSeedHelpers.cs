using IntelliMed.Core.Entities;
using IntelliMed.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Playwright;

namespace IntelliMed.Tests.Ui;

/// <summary>
/// Shared set-up steps for UI tests that need a client and/or practitioner before they can reach
/// the screen under test. Clients are created through the real Add Client screen; practitioners are
/// seeded straight into the fixture's SQLite file because no create screen or API exists for them
/// (same approach as PrescriptionWizardTests.SeedPractitionerAsync).
/// </summary>
public static class UiSeedHelpers
{
    public static AppDbContext OpenDb(this PlaywrightServerFixture fixture)
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlite($"Data Source={fixture.DbPath};Pooling=false")
            .Options;
        return new AppDbContext(options);
    }

    /// <summary>Seeds an active practitioner and returns its id. Use a unique last name per test so
    /// bookings from different tests never overlap on the same practitioner.</summary>
    public static async Task<int> SeedPractitionerAsync(this PlaywrightServerFixture fixture, string firstName, string lastName)
    {
        await using var context = fixture.OpenDb();
        var practitioner = new Practitioner { FirstName = firstName, LastName = lastName, Title = "Dr", IsActive = true };
        context.Practitioners.Add(practitioner);
        await context.SaveChangesAsync();
        return practitioner.Id;
    }

    /// <summary>Creates a client through /clients/add and returns its id (parsed from the
    /// /clients/edit/{id} URL the save lands on). The client's display name is "{given} {surname}".</summary>
    public static async Task<int> CreateClientAsync(this IPage page, string surname, string given)
    {
        await page.GotoAsync("/clients/add");
        await page.WaitForSelectorAsync("text=New Client");
        await page.Field("Surname").FillAsync(surname);
        await page.Field("Given").FillAsync(given);
        await page.GetByRole(AriaRole.Button, new() { Name = "💾 Save" }).ClickAsync();
        await page.WaitForURLAsync(url => url.Contains("/clients/edit/"));
        return IdFromUrl(page.Url);
    }

    /// <summary>The trailing integer path segment of a URL, ignoring any query string.</summary>
    public static int IdFromUrl(string url)
    {
        var path = new Uri(url).AbsolutePath.TrimEnd('/');
        return int.Parse(path[(path.LastIndexOf('/') + 1)..]);
    }

    public static string Unique(string prefix) => $"{prefix}{Guid.NewGuid():N}"[..(prefix.Length + 8)];
}
