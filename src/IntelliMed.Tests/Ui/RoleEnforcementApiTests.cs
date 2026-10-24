using System.Net;
using System.Text;
using IntelliMed.Core.Entities;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace IntelliMed.Tests.Ui;

// The UI hides what a role may not use; this proves the SERVER refuses it too. Each test calls the API
// directly as a real signed-in user of a default role and checks 403 (forbidden) against anything else
// (allowed — the call may then 400/404 on its dummy payload, which is fine: it got past the gate).
//
// The default grants are the ones a fresh database ships with:
//   Receptionist: clients (view/create/edit), appointments (view/create/edit/waiting room), invoices,
//                 invoices/create, payments, address-book
//   Doctor:       as Receptionist minus invoices/create and payments, plus clinical keys and invoices (view)
//   Nurse:        as Doctor minus invoices
//   Admin:        everything except the Clinic Settings / Clinic Manager pair
[Collection("Playwright UI")]
public class RoleEnforcementApiTests : UiTestBase
{
    private const int Missing = 987654;   // an id that does not exist, so allowed calls end in 404 rather than touching data

    public RoleEnforcementApiTests(PlaywrightServerFixture fixture) : base(fixture) { }

    private static async Task<HttpStatusCode> SendAsync(HttpClient client, HttpMethod method, string url, string? json = null)
    {
        using var request = new HttpRequestMessage(method, url);
        if (json != null) request.Content = new StringContent(json, Encoding.UTF8, "application/json");
        using var response = await client.SendAsync(request);
        return response.StatusCode;
    }

    private static Task<HttpStatusCode> Get(HttpClient c, string url) => SendAsync(c, HttpMethod.Get, url);
    private static Task<HttpStatusCode> Post(HttpClient c, string url, string json = "{}") => SendAsync(c, HttpMethod.Post, url, json);
    private static Task<HttpStatusCode> Put(HttpClient c, string url, string json = "{}") => SendAsync(c, HttpMethod.Put, url, json);
    private static Task<HttpStatusCode> Delete(HttpClient c, string url) => SendAsync(c, HttpMethod.Delete, url);

    private static void Forbidden(HttpStatusCode actual, string what) =>
        Assert.True(actual == HttpStatusCode.Forbidden, $"{what}: expected 403 Forbidden but got {(int)actual} {actual}");

    private static void Allowed(HttpStatusCode actual, string what) =>
        Assert.True(actual != HttpStatusCode.Forbidden && actual != HttpStatusCode.Unauthorized,
            $"{what}: expected to get past the permission check but got {(int)actual} {actual}");

    // ---- signed out ----

    [Fact]
    public async Task SignedOut_EveryProtectedEndpointIsRefusedWith401()
    {
        using var anonymous = new HttpClient { BaseAddress = new Uri(Fixture.BaseUrl) };

        foreach (var url in new[] { "/api/clients/search", "/api/appointments/search", "/api/invoices/search", "/api/fee-schedules/active", "/api/derived-item-configs" })
        {
            var status = await Get(anonymous, url);
            Assert.True(status == HttpStatusCode.Unauthorized, $"GET {url} signed out: expected 401 but got {(int)status}");
        }
    }

    // ---- Receptionist: front desk ----

    [Fact]
    public async Task Receptionist_CanDoTheFrontDeskJob()
    {
        var api = await Fixture.NewApiClientForRoleAsync("Receptionist");

        Allowed(await Get(api, "/api/clients/search"), "find a client");
        Allowed(await Post(api, "/api/clients"), "add a client");
        Allowed(await Put(api, $"/api/clients/{Missing}"), "edit a client");
        Allowed(await Post(api, $"/api/clients/{Missing}/allergies"), "add an allergy to a client");
        Allowed(await Get(api, "/api/appointments/search"), "list appointments");
        Allowed(await Get(api, "/api/appointments/waiting-room"), "see the waiting room");
        Allowed(await Post(api, "/api/appointments"), "book an appointment");
        Allowed(await Put(api, $"/api/appointments/{Missing}"), "edit an appointment");
        Allowed(await Put(api, $"/api/appointments/{Missing}/status", "{\"status\":1}"), "mark a patient arrived");
        Allowed(await Get(api, "/api/invoices/search"), "list invoices");
        Allowed(await Post(api, "/api/invoices"), "raise an invoice");
        Allowed(await Post(api, "/api/invoices/resolve-line"), "price an invoice line");
        Allowed(await Post(api, $"/api/invoices/{Missing}/payments"), "take a payment");
        Allowed(await Get(api, "/api/invoices/payments"), "view payments");
        Allowed(await Post(api, "/api/receipts"), "issue a receipt");
        Allowed(await Get(api, "/api/fee-schedules/active"), "read fee schedules to price an invoice");
    }

    [Fact]
    public async Task Receptionist_IsRefusedWhatTheirRoleDoesNotInclude()
    {
        var api = await Fixture.NewApiClientForRoleAsync("Receptionist");

        Forbidden(await Delete(api, $"/api/clients/{Missing}"), "archive a client");
        Forbidden(await Delete(api, $"/api/appointments/{Missing}"), "delete an appointment");
        Forbidden(await Put(api, $"/api/invoices/{Missing}/claim-status", "{\"status\":1}"), "change a claim status");
        Forbidden(await Put(api, $"/api/invoices/{Missing}/status", "{\"status\":1}"), "change an invoice status");
        Forbidden(await Post(api, "/api/fee-schedules"), "create a fee schedule");
        Forbidden(await Put(api, $"/api/fee-schedules/{Missing}"), "edit a fee schedule");
        Forbidden(await Delete(api, $"/api/fee-schedules/{Missing}"), "delete a fee schedule");
        Forbidden(await Post(api, "/api/fee-schedules/seed-bulk-bill"), "seed fee schedules");
        Forbidden(await Get(api, "/api/derived-item-configs"), "read derived item rules");
        Forbidden(await Post(api, "/api/derived-item-configs"), "create a derived item rule");
        Forbidden(await Post(api, "/api/udf-definitions"), "define a user-defined field");
        Forbidden(await Post(api, $"/api/invoices/{Missing}/write-off"), "write off an invoice");
        Forbidden(await Post(api, "/api/receipts/refund"), "refund");
    }

    // ---- Doctor: clinical, invoices read-only ----

    [Fact]
    public async Task Doctor_CanReadInvoices_ButNotRaiseOrPayThem()
    {
        var api = await Fixture.NewApiClientForRoleAsync("Doctor");

        Allowed(await Get(api, "/api/invoices/search"), "list invoices");
        Allowed(await Get(api, "/api/clients/search"), "find a client");
        Allowed(await Get(api, "/api/appointments/waiting-room"), "see the waiting room");
        Allowed(await Put(api, $"/api/appointments/{Missing}"), "edit an appointment");

        Forbidden(await Post(api, "/api/invoices"), "raise an invoice");
        Forbidden(await Post(api, $"/api/invoices/{Missing}/payments"), "take a payment");
        Forbidden(await Get(api, "/api/invoices/payments"), "view payments");
        Forbidden(await Delete(api, $"/api/clients/{Missing}"), "archive a client");
        Forbidden(await Delete(api, $"/api/appointments/{Missing}"), "delete an appointment");
        Forbidden(await Post(api, "/api/fee-schedules"), "create a fee schedule");
    }

    // ---- Nurse: clinical, no billing ----

    [Fact]
    public async Task Nurse_HasNoBillingAccess_ButCanWorkWithClientsAndAppointments()
    {
        var api = await Fixture.NewApiClientForRoleAsync("Nurse");

        Allowed(await Get(api, "/api/clients/search"), "find a client");
        Allowed(await Post(api, "/api/clients"), "add a client");
        Allowed(await Get(api, "/api/appointments/search"), "list appointments");
        Allowed(await Post(api, "/api/appointments"), "book an appointment");

        Forbidden(await Get(api, "/api/invoices/search"), "list invoices");
        Forbidden(await Post(api, "/api/invoices"), "raise an invoice");
        Forbidden(await Get(api, "/api/invoices/payments"), "view payments");
        Forbidden(await Post(api, "/api/receipts"), "issue a receipt");
        Forbidden(await Get(api, "/api/fee-schedules/search"), "list fee schedules");
        Forbidden(await Delete(api, $"/api/clients/{Missing}"), "archive a client");
    }

    // ---- Admin: everything the keys grant ----

    [Fact]
    public async Task Admin_IsAllowedTheDestructiveAndConfigurationActions()
    {
        var api = await Fixture.NewApiClientForRoleAsync("Admin");

        Allowed(await Delete(api, $"/api/clients/{Missing}"), "archive a client");
        Allowed(await Delete(api, $"/api/appointments/{Missing}"), "delete an appointment");
        Allowed(await Post(api, "/api/fee-schedules"), "create a fee schedule");
        Allowed(await Get(api, "/api/derived-item-configs"), "read derived item rules");
        Allowed(await Put(api, $"/api/invoices/{Missing}/claim-status", "{\"status\":1}"), "change a claim status");
        Allowed(await Post(api, $"/api/invoices/{Missing}/write-off"), "write off an invoice");
        Allowed(await Post(api, "/api/udf-definitions"), "define a user-defined field");
    }

    // ---- the key, not the role name, is what decides ----

    private async Task WithGrantAsync(string role, string pageKey, Func<Task> act)
    {
        await using (var db = Fixture.OpenDb())
        {
            if (!await db.RolePermissions.AnyAsync(p => p.RoleName == role && p.PageKey == pageKey))
            {
                db.RolePermissions.Add(new RolePermission { RoleName = role, PageKey = pageKey, Category = "Clinical" });
                await db.SaveChangesAsync();
            }
        }
        try
        {
            await act();
        }
        finally
        {
            await using var db = Fixture.OpenDb();
            await db.RolePermissions.Where(p => p.RoleName == role && p.PageKey == pageKey).ExecuteDeleteAsync();
        }
    }

    private async Task WithRevokeAsync(string role, string pageKey, Func<Task> act)
    {
        await using (var db = Fixture.OpenDb())
        {
            await db.RolePermissions.Where(p => p.RoleName == role && p.PageKey == pageKey).ExecuteDeleteAsync();
        }
        try
        {
            await act();
        }
        finally
        {
            await using var db = Fixture.OpenDb();
            if (!await db.RolePermissions.AnyAsync(p => p.RoleName == role && p.PageKey == pageKey))
            {
                db.RolePermissions.Add(new RolePermission { RoleName = role, PageKey = pageKey, Category = "Clinical" });
                await db.SaveChangesAsync();
            }
        }
    }

    [Fact]
    public async Task GrantingAKey_OpensTheEndpoint_AndRemovingItClosesItAgain()
    {
        var api = await Fixture.NewApiClientForRoleAsync("Receptionist");
        Forbidden(await Delete(api, $"/api/clients/{Missing}"), "archive a client, before any grant");

        await WithGrantAsync("Receptionist", "clients/delete", async () =>
        {
            Allowed(await Delete(api, $"/api/clients/{Missing}"), "archive a client, with clients/delete granted");
        });

        Forbidden(await Delete(api, $"/api/clients/{Missing}"), "archive a client, after the grant is removed");
    }

    [Fact]
    public async Task RevokingAKeyFromADefaultRole_ClosesTheEndpoint_AndRestoringItReopensIt()
    {
        var api = await Fixture.NewApiClientForRoleAsync("Doctor");
        Allowed(await Get(api, "/api/invoices/search"), "list invoices, by default");

        await WithRevokeAsync("Doctor", "invoices", async () =>
        {
            Forbidden(await Get(api, "/api/invoices/search"), "list invoices, with invoices revoked");
        });

        Allowed(await Get(api, "/api/invoices/search"), "list invoices, once restored");
    }

    [Fact]
    public async Task ASharedEndpoint_IsOpenedByAnyOfTheFeaturesThatNeedIt()
    {
        // Looking a client up is needed to book an appointment, so a role holding only that key — and not
        // "clients" — must still be able to find the patient it is booking for.
        var api = await Fixture.NewApiClientForRoleAsync("Nurse");

        await WithRevokeAsync("Nurse", "clients", async () =>
        {
            Allowed(await Get(api, "/api/clients/search"), "client lookup via appointments/progress-notes/results keys");
        });
    }
}
