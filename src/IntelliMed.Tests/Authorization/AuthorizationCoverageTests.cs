using System.Reflection;
using FluentAssertions;
using IntelliMed.Api.Authorization;
using IntelliMed.Api.Controllers;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Routing;
using Xunit;

namespace IntelliMed.Tests.Authorization;

/// <summary>
/// The guard against the gap this suite was written to close: for most of the app, a role's page keys
/// used to hide sidebar links and nothing more, because no controller action checked them. These tests
/// fail when a NEW controller action is added with no authorization beyond "signed in", or when a
/// permission-catalogue key is added that nothing enforces — so the catalogue cannot quietly drift away
/// from what the API actually does again.
/// </summary>
public class AuthorizationCoverageTests
{
    /// <summary>Controllers that do their own page-key check inside the action (so the source must
    /// still call HasAccessAsync / RequirePage — verified below). Keep this list shrinking: prefer
    /// [RequirePage] on new work.</summary>
    private static readonly HashSet<string> InlinePageCheck = new()
    {
        "AddressBook", "AppointmentTypeSettings", "BillingItems", "ClientHealthIssues", "ClinicSettings",
        "Clinics", "DrugInteractions", "EmailTemplates", "Episodes", "Medicines", "PrescriptionIssues",
        "Prescriptions", "ProgressNotes", "Results", "SearchActions", "SystemSettings",
    };

    /// <summary>Controllers that are deliberately reachable by any signed-in user (or by nobody in
    /// particular), with the reason. Adding to this list should be a conscious decision.</summary>
    private static readonly Dictionary<string, string> OpenByDesign = new()
    {
        ["Practitioners"] = "reference lookup (names of active practitioners) used by every booking and billing screen",
        ["HealthFunds"] = "reference lookup of health funds",
        ["ProviderGroups"] = "reference lookup of provider groups",
        ["Notifications"] = "each user's own notifications; scoped to the caller inside the action",
        ["DbAdmin"] = "separate credential and scope token, deliberately outside the staff permission system",
        ["Auth"] = "sign-in, password reset and the caller's own profile",
        ["Log"] = "front-end error logging, which must work before sign-in",
        ["Version"] = "version endpoint used to spot a stale deployment",
    };

    /// <summary>Individual actions that are open to any signed-in user, as "Controller.Action".</summary>
    private static readonly Dictionary<string, string> OpenActions = new()
    {
        ["RolePermissions.GetMyAccessiblePages"] = "tells the caller which pages THEY hold, for the sidebar",
    };

    /// <summary>Catalogue keys the API does not itself enforce, and why. A key added to the catalogue that
    /// nothing checks must be listed here (and explained) or the test below fails.</summary>
    private static readonly Dictionary<string, string> NotKeyEnforced = new()
    {
        ["admin/users"] = "User Management is SuperAdmin/Admin only by role attribute — delegating it is a privilege-escalation risk",
        ["admin/roles"] = "Role Configuration is SuperAdmin/Admin only by role attribute — same reason",
        ["admin/database-backups"] = "Database Backups is SuperAdmin/Admin only by role attribute",
        ["invoices/delete"] = "there is no delete-invoice action — an invoice is cancelled (invoices/void), never deleted",
        ["reports"] = "no report screens exist yet",
        ["reports/financial"] = "no report screens exist yet",
        ["reports/clinical"] = "no report screens exist yet",
    };

    private static readonly Assembly Api = typeof(RolePermissionsController).Assembly;

    private static IEnumerable<Type> Controllers() =>
        Api.GetTypes().Where(t => !t.IsAbstract && typeof(ControllerBase).IsAssignableFrom(t));

    private static string Name(Type t) => t.Name.Replace("Controller", string.Empty);

    private static bool IsAction(MethodInfo m) =>
        m.IsPublic && !m.IsStatic && m.GetCustomAttributes<HttpMethodAttribute>(inherit: true).Any();

    private static IEnumerable<MethodInfo> Actions(Type t) =>
        t.GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly).Where(IsAction);

    private static bool HasRolesAuthorize(MemberInfo m) =>
        m.GetCustomAttributes<AuthorizeAttribute>(inherit: true).Any(a => !string.IsNullOrWhiteSpace(a.Roles));

    private static bool IsProtected(Type controller, MethodInfo action)
    {
        var name = Name(controller);
        return controller.GetCustomAttributes<RequirePageAttribute>(true).Any()
               || action.GetCustomAttributes<RequirePageAttribute>(true).Any()
               || controller.GetCustomAttributes<AllowAnonymousAttribute>(true).Any()
               || action.GetCustomAttributes<AllowAnonymousAttribute>(true).Any()
               || HasRolesAuthorize(controller)
               || HasRolesAuthorize(action)
               || InlinePageCheck.Contains(name)
               || OpenByDesign.ContainsKey(name)
               || OpenActions.ContainsKey($"{name}.{action.Name}");
    }

    private static string RepoRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir != null && !File.Exists(Path.Combine(dir.FullName, "IntelliMed.sln")))
            dir = dir.Parent;
        return dir?.FullName ?? throw new InvalidOperationException("IntelliMed.sln not found above the test output.");
    }

    private static string ControllersDir => Path.Combine(RepoRoot(), "src", "IntelliMed.Api", "Controllers");

    // ---- every action is protected beyond "signed in" ----

    [Fact]
    public void EveryControllerAction_RequiresMoreThanBeingSignedIn()
    {
        var unprotected = Controllers()
            .SelectMany(c => Actions(c).Where(a => !IsProtected(c, a)).Select(a => $"{Name(c)}.{a.Name}"))
            .OrderBy(x => x)
            .ToList();

        unprotected.Should().BeEmpty(
            "every action needs [RequirePage(\"...\")] (or a deliberate entry in this test's allow-lists) so a role's " +
            "permissions bind the API, not just the sidebar. Unprotected: " + string.Join(", ", unprotected));
    }

    [Fact]
    public void TheControllerScanFindsTheActionsItShould()
    {
        // Guards the reflection above against silently matching nothing.
        Controllers().Should().HaveCountGreaterThan(30);
        Controllers().SelectMany(Actions).Should().HaveCountGreaterThan(200);
        Actions(typeof(ClientsController)).Should().Contain(a => a.Name == "Archive");
    }

    [Fact]
    public void ClientsAndAppointmentsAndInvoices_AreGatedPerAction_NotJustSignedIn()
    {
        foreach (var type in new[] { typeof(ClientsController), typeof(AppointmentsController), typeof(InvoicesController), typeof(FeeSchedulesController) })
        {
            foreach (var action in Actions(type))
            {
                var gated = action.GetCustomAttributes<RequirePageAttribute>(true).Any() || HasRolesAuthorize(action);
                gated.Should().BeTrue($"{Name(type)}.{action.Name} must carry its own gate");
            }
        }
    }

    // ---- the allow-lists are honest ----

    [Fact]
    public void EveryControllerOnTheInlineList_ReallyChecksAPageKey()
    {
        foreach (var name in InlinePageCheck)
        {
            var source = File.ReadAllText(Path.Combine(ControllersDir, $"{name}Controller.cs"));

            (source.Contains("HasAccessAsync") || source.Contains("RequirePage"))
                .Should().BeTrue($"{name}Controller is on the inline-check list but its source never checks a page key");
        }
    }

    [Fact]
    public void TheAllowLists_OnlyNameControllersAndActionsThatExist()
    {
        var names = Controllers().Select(Name).ToHashSet();
        InlinePageCheck.Concat(OpenByDesign.Keys).Where(n => !names.Contains(n)).Should().BeEmpty();

        var actions = Controllers().SelectMany(c => Actions(c).Select(a => $"{Name(c)}.{a.Name}")).ToHashSet();
        OpenActions.Keys.Where(a => !actions.Contains(a)).Should().BeEmpty();
    }

    [Fact]
    public void NoControllerIsOnBothAllowLists()
    {
        InlinePageCheck.Intersect(OpenByDesign.Keys).Should().BeEmpty();
    }

    // ---- the catalogue and the API agree ----

    private static IEnumerable<string> KeysUsedByTheApi()
    {
        var fromAttributes = Controllers()
            .SelectMany(c => c.GetCustomAttributes<RequirePageAttribute>(true)
                .Concat(Actions(c).SelectMany(a => a.GetCustomAttributes<RequirePageAttribute>(true))))
            .SelectMany(a => a.Keys);

        var fromConstants = typeof(PageAccess)
            .GetFields(BindingFlags.Public | BindingFlags.Static)
            .Where(f => f.IsLiteral && f.FieldType == typeof(string))
            .SelectMany(f => ((string)f.GetRawConstantValue()!).Split(RequirePageAttribute.Separator, StringSplitOptions.RemoveEmptyEntries));

        return fromAttributes.Concat(fromConstants).Distinct();
    }

    [Fact]
    public void EveryKeyTheApiRequires_IsInThePermissionCatalogue()
    {
        var catalogue = RolePermissionsController.GetPageCatalogue().Select(p => p.PageKey).ToHashSet();

        KeysUsedByTheApi().Where(k => !catalogue.Contains(k)).Should().BeEmpty(
            "a key missing from the catalogue can never be granted in Role Configuration, so the endpoint would be SuperAdmin-only by accident");
    }

    [Fact]
    public void EveryCatalogueKey_IsEnforcedByTheApi_OrExplainedAsNot()
    {
        var catalogue = RolePermissionsController.GetPageCatalogue().Select(p => p.PageKey).ToList();

        // A key counts as enforced when the API source mentions it as a quoted literal anywhere other than
        // the catalogue itself — a [RequirePage], a PageAccess set, or an inline HasAccessAsync/PageKey.
        // Comment lines are dropped: a key that is only MENTIONED in a doc comment is not enforced.
        static string Code(string path) =>
            string.Join(Environment.NewLine, File.ReadAllLines(path).Where(l => !l.TrimStart().StartsWith("//")));

        var source = string.Concat(
            Directory.GetFiles(ControllersDir, "*.cs")
                .Where(f => !f.EndsWith("RolePermissionsController.cs"))
                .Select(Code)) +
            Code(Path.Combine(RepoRoot(), "src", "IntelliMed.Api", "Authorization", "PageAccess.cs"));

        var unenforced = catalogue.Where(k => !source.Contains($"\"{k}\"") && !source.Contains($"|{k}|") && !source.Contains($"|{k}\"") && !source.Contains($"\"{k}|"))
            .ToHashSet();

        unenforced.Except(NotKeyEnforced.Keys).Should().BeEmpty(
            "a catalogue key that no controller checks looks configurable in Role Configuration but does nothing. " +
            "Enforce it, or add it to NotKeyEnforced with the reason.");
        NotKeyEnforced.Keys.Except(unenforced).Should().BeEmpty(
            "these keys are now enforced — remove them from NotKeyEnforced");
        NotKeyEnforced.Keys.Except(catalogue).Should().BeEmpty();
    }
}
