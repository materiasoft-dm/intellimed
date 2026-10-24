using FluentAssertions;
using IntelliMed.Api.Controllers;
using IntelliMed.Core.DTOs;
using IntelliMed.Core.Permissions;
using Xunit;

namespace IntelliMed.Tests.Authorization;

public class PermissionTreeTests
{
    private static PageDefinitionDto Page(string key, string name, string category, string? group = null, string? action = null, string description = "") =>
        new() { PageKey = key, PageName = name, Category = category, Group = group, Action = action, Description = description };

    private static List<PageDefinitionDto> SamplePages() => new()
    {
        Page("appointments", "Appointments", "Clinical", "Appointments", "View"),
        Page("appointments/create", "New Appointment", "Clinical", "Appointments", "Create"),
        Page("appointments/edit", "Edit Appointment", "Clinical", "Appointments", "Update"),
        Page("appointments/delete", "Delete Appointment", "Clinical", "Appointments", "Remove"),
        Page("address-book", "Address Book", "Clinical"),
        Page("payments", "Payments", "Financial"),
        Page("invoices", "Invoices", "Financial", "Invoices", "View"),
        Page("invoices/create", "New Invoice", "Financial", "Invoices", "Create"),
    };

    private static PermissionNode Find(IEnumerable<PermissionNode> nodes, string id)
    {
        foreach (var n in nodes)
        {
            if (n.Id == id) return n;
            var inner = n.Children.Count > 0 ? FindOrNull(n.Children, id) : null;
            if (inner != null) return inner;
        }
        throw new InvalidOperationException($"node '{id}' not found");
    }

    private static PermissionNode? FindOrNull(IEnumerable<PermissionNode> nodes, string id)
    {
        foreach (var n in nodes)
        {
            if (n.Id == id) return n;
            var inner = FindOrNull(n.Children, id);
            if (inner != null) return inner;
        }
        return null;
    }

    private static HashSet<string> Selected(params string[] keys) => new(keys, StringComparer.OrdinalIgnoreCase);

    // ---- Build ----

    [Fact]
    public void Build_NestsCategoryThenGroupThenPermission()
    {
        var tree = PermissionTree.Build(SamplePages());

        tree.Select(n => n.Label).Should().Equal("Clinical", "Financial");
        var appointments = Find(tree, "group:Clinical/Appointments");
        appointments.Label.Should().Be("Appointments");
        appointments.IsLeaf.Should().BeFalse();
        appointments.Children.Select(c => c.Label).Should().Equal("View", "Create", "Update", "Remove");
        appointments.Children.Select(c => c.PageKey).Should().Equal(
            "appointments", "appointments/create", "appointments/edit", "appointments/delete");
    }

    [Fact]
    public void Build_AGroupOfOnePermission_IsShownAsThatPermission_NotAOneChildParent()
    {
        var tree = PermissionTree.Build(SamplePages());

        var addressBook = Find(tree, "address-book");
        addressBook.IsLeaf.Should().BeTrue();
        addressBook.Label.Should().Be("Address Book");
        tree[0].Children.Should().Contain(addressBook);
    }

    [Fact]
    public void Build_PreservesCatalogueOrder()
    {
        var tree = PermissionTree.Build(SamplePages());

        tree[0].Children.Select(c => c.Label).Should().Equal("Appointments", "Address Book");
        tree[1].Children.Select(c => c.Label).Should().Equal("Payments", "Invoices");
    }

    [Fact]
    public void Build_AMemberWithoutAnAction_FallsBackToItsPageName()
    {
        var pages = new List<PageDefinitionDto>
        {
            Page("reports", "Reports Dashboard", "Reports", "Reports", "Dashboard"),
            Page("reports/clinical", "Clinical Reports", "Reports", "Reports")
        };

        var group = Find(PermissionTree.Build(pages), "group:Reports/Reports");

        group.Children.Select(c => c.Label).Should().Equal("Dashboard", "Clinical Reports");
    }

    [Fact]
    public void LeafKeys_OfABranch_AreEveryPermissionBeneathIt()
    {
        var tree = PermissionTree.Build(SamplePages());

        Find(tree, "group:Clinical/Appointments").LeafKeys.Should().HaveCount(4);
        Find(tree, "category:Clinical").LeafKeys.Should().HaveCount(5);
        Find(tree, "category:Financial").LeafKeys.Should().BeEquivalentTo("payments", "invoices", "invoices/create");
    }

    // ---- StateOf ----

    [Fact]
    public void StateOf_IsNoneWhenNothingUnderTheNodeIsGranted()
    {
        var group = Find(PermissionTree.Build(SamplePages()), "group:Clinical/Appointments");

        PermissionTree.StateOf(group, Selected("payments")).Should().Be(CheckState.None);
    }

    [Fact]
    public void StateOf_IsSomeWhenOnlySomeAreGranted()
    {
        var group = Find(PermissionTree.Build(SamplePages()), "group:Clinical/Appointments");

        PermissionTree.StateOf(group, Selected("appointments/create")).Should().Be(CheckState.Some);
        PermissionTree.StateOf(group, Selected("appointments", "appointments/create", "appointments/edit")).Should().Be(CheckState.Some);
    }

    [Fact]
    public void StateOf_IsAllOnlyWhenEveryPermissionIsGranted()
    {
        var group = Find(PermissionTree.Build(SamplePages()), "group:Clinical/Appointments");

        PermissionTree.StateOf(group, Selected("appointments", "appointments/create", "appointments/edit", "appointments/delete"))
            .Should().Be(CheckState.All);
    }

    [Fact]
    public void StateOf_ACategory_IsSomeWhenOnlyOneOfItsGroupsIsFullyGranted()
    {
        var clinical = Find(PermissionTree.Build(SamplePages()), "category:Clinical");

        var onlyAppointments = Selected("appointments", "appointments/create", "appointments/edit", "appointments/delete");

        PermissionTree.StateOf(clinical, onlyAppointments).Should().Be(CheckState.Some);
    }

    [Fact]
    public void StateOf_ALeaf_IsAllOrNone()
    {
        var leaf = Find(PermissionTree.Build(SamplePages()), "payments");

        PermissionTree.StateOf(leaf, Selected("payments")).Should().Be(CheckState.All);
        PermissionTree.StateOf(leaf, Selected()).Should().Be(CheckState.None);
    }

    [Fact]
    public void StateOf_IsCaseInsensitiveWhenTheSetIs()
    {
        var leaf = Find(PermissionTree.Build(SamplePages()), "payments");

        PermissionTree.StateOf(leaf, Selected("PAYMENTS")).Should().Be(CheckState.All);
    }

    // ---- Toggle ----

    [Fact]
    public void Toggle_TickingAParent_GrantsEverythingUnderIt_AndNothingElse()
    {
        var group = Find(PermissionTree.Build(SamplePages()), "group:Clinical/Appointments");
        var selected = Selected("payments");

        PermissionTree.Toggle(group, selected, isChecked: true);

        selected.Should().BeEquivalentTo(
            "payments", "appointments", "appointments/create", "appointments/edit", "appointments/delete");
    }

    [Fact]
    public void Toggle_UntickingAParent_RevokesEverythingUnderIt_AndNothingElse()
    {
        var group = Find(PermissionTree.Build(SamplePages()), "group:Clinical/Appointments");
        var selected = Selected("payments", "appointments", "appointments/create", "appointments/edit", "appointments/delete");

        PermissionTree.Toggle(group, selected, isChecked: false);

        selected.Should().BeEquivalentTo("payments");
    }

    [Fact]
    public void Toggle_APartlyTickedParent_BecomesFullyTicked()
    {
        var group = Find(PermissionTree.Build(SamplePages()), "group:Clinical/Appointments");
        var selected = Selected("appointments/create");

        // What a click on a partly ticked box does in the browser: it becomes ticked.
        PermissionTree.Toggle(group, selected, isChecked: true);

        PermissionTree.StateOf(group, selected).Should().Be(CheckState.All);
    }

    [Fact]
    public void Toggle_ACategory_ReachesEveryGroupBeneathIt()
    {
        var clinical = Find(PermissionTree.Build(SamplePages()), "category:Clinical");
        var selected = Selected();

        PermissionTree.Toggle(clinical, selected, isChecked: true);

        selected.Should().BeEquivalentTo("appointments", "appointments/create", "appointments/edit", "appointments/delete", "address-book");
    }

    [Fact]
    public void Toggle_ThenUntickingOneChild_LeavesTheParentPartlyTicked()
    {
        var tree = PermissionTree.Build(SamplePages());
        var group = Find(tree, "group:Clinical/Appointments");
        var selected = Selected();
        PermissionTree.Toggle(group, selected, true);

        PermissionTree.Toggle(Find(tree, "appointments/delete"), selected, false);

        PermissionTree.StateOf(group, selected).Should().Be(CheckState.Some);
        PermissionTree.StateOf(Find(tree, "category:Clinical"), selected).Should().Be(CheckState.Some);
    }

    // ---- Filter ----

    [Fact]
    public void Filter_WithNoTerm_ReturnsEverything()
    {
        var tree = PermissionTree.Build(SamplePages());

        PermissionTree.Filter(tree, null).Should().HaveCount(2);
        PermissionTree.Filter(tree, "   ").Should().HaveCount(2);
    }

    [Fact]
    public void Filter_KeepsTheAncestorsOfAMatchingPermission()
    {
        var tree = PermissionTree.Build(SamplePages());

        var filtered = PermissionTree.Filter(tree, "Remove");

        filtered.Should().ContainSingle(n => n.Id == "category:Clinical");
        var group = Find(filtered, "group:Clinical/Appointments");
        group.Children.Should().ContainSingle().Which.PageKey.Should().Be("appointments/delete");
    }

    [Fact]
    public void Filter_AMatchOnAGroupName_KeepsAllItsPermissions()
    {
        var tree = PermissionTree.Build(SamplePages());

        var group = Find(PermissionTree.Filter(tree, "appointments"), "group:Clinical/Appointments");

        group.Children.Should().HaveCount(4);
    }

    [Fact]
    public void Filter_MatchesOnPageKeyAndDescription()
    {
        var pages = new List<PageDefinitionDto>
        {
            Page("admin/medicines", "Medicine Catalog", "Admin", description: "Manage the medicines reference-data catalog"),
            Page("admin/roles", "Role Configuration", "Admin", description: "Configure role permissions"),
        };
        var tree = PermissionTree.Build(pages);

        PermissionTree.Filter(tree, "admin/roles").Single().Children.Should().ContainSingle().Which.PageKey.Should().Be("admin/roles");
        PermissionTree.Filter(tree, "reference-data").Single().Children.Should().ContainSingle().Which.PageKey.Should().Be("admin/medicines");
    }

    [Fact]
    public void Filter_WithNoMatch_ReturnsNothing()
    {
        PermissionTree.Filter(PermissionTree.Build(SamplePages()), "zzz-nothing").Should().BeEmpty();
    }

    [Fact]
    public void Filter_ADifferentNarrowedParent_StillReportsTheStateOfAllItsPermissions()
    {
        var tree = PermissionTree.Build(SamplePages());
        var selected = Selected("appointments/create", "appointments/edit", "appointments/delete");   // all but View

        var group = Find(PermissionTree.Filter(tree, "Remove"), "group:Clinical/Appointments");

        // Only "Remove" is visible, but the parent must not claim it is fully ticked.
        group.Children.Should().HaveCount(1);
        PermissionTree.StateOf(group, selected).Should().Be(CheckState.Some);
        group.LeafKeys.Should().HaveCount(4);
    }

    [Fact]
    public void Filter_DoesNotChangeTheOriginalTree()
    {
        var tree = PermissionTree.Build(SamplePages());

        PermissionTree.Filter(tree, "Remove");

        Find(tree, "group:Clinical/Appointments").Children.Should().HaveCount(4);
    }

    // ---- the real catalogue ----

    [Fact]
    public void Catalogue_HasUniqueKeys_AndEveryEntryIsCategorised()
    {
        var catalogue = RolePermissionsController.GetPageCatalogue();

        catalogue.Select(p => p.PageKey).Should().OnlyHaveUniqueItems();
        catalogue.Should().OnlyContain(p => !string.IsNullOrWhiteSpace(p.PageName) && !string.IsNullOrWhiteSpace(p.Category));
    }

    [Fact]
    public void Catalogue_EveryMemberOfAMultiPermissionGroup_HasAUniqueActionLabel()
    {
        var catalogue = RolePermissionsController.GetPageCatalogue();

        foreach (var group in catalogue.Where(p => p.Group != null).GroupBy(p => (p.Category, p.Group)))
        {
            group.Should().OnlyContain(p => !string.IsNullOrWhiteSpace(p.Action), $"group '{group.Key.Group}' members need a label");
            group.Select(p => p.Action).Should().OnlyHaveUniqueItems($"two permissions in '{group.Key.Group}' cannot share a label");
        }
    }

    [Fact]
    public void Catalogue_AppointmentsFollowsTheViewCreateUpdateRemovePattern()
    {
        var tree = PermissionTree.Build(RolePermissionsController.GetPageCatalogue());

        var appointments = Find(tree, "group:Clinical/Appointments");

        appointments.Children.Select(c => c.Label).Should().Contain(new[] { "View", "Create", "Update", "Remove" });
        appointments.Children.Single(c => c.Label == "View").PageKey.Should().Be("appointments");
        appointments.Children.Single(c => c.Label == "Remove").PageKey.Should().Be("appointments/delete");
    }

    [Fact]
    public void Catalogue_BuildsATreeContainingEveryPermissionExactlyOnce()
    {
        var catalogue = RolePermissionsController.GetPageCatalogue();
        var tree = PermissionTree.Build(catalogue);

        tree.SelectMany(r => r.LeafKeys).Should().BeEquivalentTo(catalogue.Select(p => p.PageKey));
        tree.SelectMany(r => r.LeafKeys).Should().OnlyHaveUniqueItems();
    }
}
