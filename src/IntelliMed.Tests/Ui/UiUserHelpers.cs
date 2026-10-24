using System.Net.Http.Json;
using Microsoft.Playwright;

namespace IntelliMed.Tests.Ui;

/// <summary>
/// Creates throwaway users through the real User Management screen, for tests that need to see the
/// app as a role other than the seeded SuperAdmin (RBAC-filtered navigation, hidden actions).
/// Mirrors the flow CommandPaletteTests.NonAdminRole_DoesNotSeeAdminOnlyActions uses inline.
/// </summary>
public static class UiUserHelpers
{
    public const string TestUserPassword = "PlaywrightPass123!";

    /// <summary>Creates a user (first name "Playwright") through the Add User dialog on the given
    /// admin page and returns its email, "{lastName lower-case}@example.com". Leaves the page on
    /// /admin/users with the dialog closed.</summary>
    public static async Task<string> CreateUserAsync(this IPage adminPage, string lastName, string roleLabel = "Nurse")
    {
        var email = $"{lastName.ToLowerInvariant()}@example.com";
        await adminPage.GotoAsync("/admin/users");
        await adminPage.GetByRole(AriaRole.Heading, new() { Name = "User Management" }).WaitForAsync();
        await adminPage.GetByRole(AriaRole.Button, new() { Name = "Add User" }).ClickAsync();
        await adminPage.Field("First Name").FillAsync("Playwright");
        await adminPage.Field("Last Name").FillAsync(lastName);
        await adminPage.Field("Email Address").FillAsync(email);
        await adminPage.GetByLabel(roleLabel, new() { Exact = true }).CheckAsync();
        await adminPage.GetByRole(AriaRole.Button, new() { Name = "Create User" }).ClickAsync();
        await adminPage.WaitForSelectorAsync("text=created successfully");
        await adminPage.GetByRole(AriaRole.Button, new() { Name = "Done" }).ClickAsync();
        return email;
    }

    /// <summary>Creates a user holding <paramref name="roleLabel"/> (the checkbox label in the Add User
    /// dialog, e.g. "Nurse"), gives it <see cref="TestUserPassword"/> via Reset Password, and returns a
    /// fresh page signed in as that user.</summary>
    public static async Task<IPage> NewPageSignedInAsRoleAsync(this PlaywrightServerFixture fixture, string roleLabel)
    {
        var adminPage = await fixture.NewAuthenticatedPageAsync();
        var email = await adminPage.CreateUserAsync($"{roleLabel.Replace(" ", "")}{Guid.NewGuid():N}"[..(roleLabel.Length + 8)], roleLabel);

        await adminPage.Locator("tr", new PageLocatorOptions { HasTextString = email })
            .GetByTitle("Reset Password")
            .ClickAsync();
        await adminPage.Locator("input[type='password']").FillAsync(TestUserPassword);
        await adminPage.GetByRole(AriaRole.Button, new() { Name = "Reset Password" }).ClickAsync();
        await adminPage.WaitForSelectorAsync("text=Password reset successfully.");

        var page = await fixture.NewPageAsync();
        await page.GotoAsync("/login");
        await page.Locator("#email").FillAsync(email);
        await page.Locator("#password").FillAsync(TestUserPassword);
        await page.GetByRole(AriaRole.Button, new() { Name = "Sign In" }).ClickAsync();
        await page.WaitForURLAsync(url => !url.Contains("/login"));
        return page;
    }

    private static readonly System.Collections.Concurrent.ConcurrentDictionary<string, Task<HttpClient>> ApiClients = new();

    /// <summary>
    /// An <see cref="HttpClient"/> for the test server, signed in (bearer token) as a throwaway user
    /// holding <paramref name="roleLabel"/>. For asserting what the API itself allows a role — the UI
    /// hides things, but only a direct call shows whether the server would have stopped it. One user is
    /// created per role and reused for the rest of the run.
    /// </summary>
    public static async Task<HttpClient> NewApiClientForRoleAsync(this PlaywrightServerFixture fixture, string roleLabel)
    {
        var key = $"{fixture.BaseUrl}|{roleLabel}";
        try
        {
            return await ApiClients.GetOrAdd(key, _ => CreateApiClientAsync(fixture, roleLabel));
        }
        catch
        {
            // Never cache a failed setup, or every later test for this role would fail instantly on it.
            ApiClients.TryRemove(key, out _);
            throw;
        }
    }

    private static async Task<HttpClient> CreateApiClientAsync(PlaywrightServerFixture fixture, string roleLabel)
    {
        var adminPage = await fixture.NewAuthenticatedPageAsync();
        var email = await adminPage.CreateUserAsync($"Api{roleLabel.Replace(" ", "")}{Guid.NewGuid():N}"[..(roleLabel.Length + 11)], roleLabel);

        await adminPage.Locator("tr", new PageLocatorOptions { HasTextString = email })
            .GetByTitle("Reset Password")
            .ClickAsync();
        await adminPage.Locator("input[type='password']").FillAsync(TestUserPassword);
        await adminPage.GetByRole(AriaRole.Button, new() { Name = "Reset Password" }).ClickAsync();
        await adminPage.WaitForSelectorAsync("text=Password reset successfully.");

        var client = new HttpClient { BaseAddress = new Uri(fixture.BaseUrl) };
        var response = await client.PostAsJsonAsync("/api/auth/login", new { email, password = TestUserPassword });
        response.EnsureSuccessStatusCode();
        var login = await response.Content.ReadFromJsonAsync<System.Text.Json.JsonElement>();
        var token = login.GetProperty("token").GetString();
        client.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);
        return client;
    }
}
