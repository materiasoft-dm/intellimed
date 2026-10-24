using IntelliMed.Core.DTOs;

namespace IntelliMed.Core.Permissions;

public enum CheckState
{
    /// <summary>Nothing under the node is granted.</summary>
    None,

    /// <summary>Some, but not all, of what is under the node is granted — shown as a partial tick.</summary>
    Some,

    /// <summary>Everything under the node is granted.</summary>
    All
}

/// <summary>
/// One row in the Role Configuration tree. A leaf carries a <see cref="PageKey"/> (a permission that
/// is actually stored); a branch (category or group) has none and exists only to group and bulk-toggle
/// the leaves under it.
/// </summary>
public class PermissionNode
{
    private IReadOnlyList<string>? _leafKeys;

    public string Id { get; init; } = string.Empty;
    public string Label { get; init; } = string.Empty;
    public string? Description { get; init; }

    /// <summary>The stored permission key. Null for a branch.</summary>
    public string? PageKey { get; init; }

    public List<PermissionNode> Children { get; init; } = new();

    public bool IsLeaf => PageKey != null;

    /// <summary>Every permission key at or beneath this node. For a filtered copy this is still the
    /// full set, so a parent's tick state does not change while a search narrows what is visible.</summary>
    public IReadOnlyList<string> LeafKeys
    {
        get => _leafKeys ??= IsLeaf ? new[] { PageKey! } : Children.SelectMany(c => c.LeafKeys).ToList();
        init => _leafKeys = value;
    }
}

/// <summary>
/// Builds the permission hierarchy from the flat page catalogue and works out its tick states.
///
/// The model is deliberately presentation-only: what is stored (and enforced) stays a flat set of page
/// keys per role, so a parent never "has" a permission of its own — it is checked exactly when every
/// permission beneath it is, and partially checked when only some are.
/// </summary>
public static class PermissionTree
{
    /// <summary>Category → group → permission. Ordering follows the catalogue, so the order in which
    /// pages are declared is the order an admin sees.</summary>
    public static List<PermissionNode> Build(IEnumerable<PageDefinitionDto> pages)
    {
        var roots = new List<PermissionNode>();

        foreach (var category in pages.GroupBy(p => p.Category))
        {
            var children = new List<PermissionNode>();

            // Pages without a Group stand alone, so key those on their own page key.
            foreach (var group in category.GroupBy(p => p.Group ?? p.PageKey))
            {
                var members = group.ToList();
                if (members.Count == 1)
                {
                    children.Add(Leaf(members[0], members[0].PageName));
                    continue;
                }

                children.Add(new PermissionNode
                {
                    Id = $"group:{category.Key}/{group.Key}",
                    Label = group.Key,
                    Children = members.Select(m => Leaf(m, m.Action ?? m.PageName)).ToList()
                });
            }

            roots.Add(new PermissionNode
            {
                Id = $"category:{category.Key}",
                Label = category.Key,
                Children = children
            });
        }

        return roots;
    }

    private static PermissionNode Leaf(PageDefinitionDto page, string label) => new()
    {
        Id = page.PageKey,
        PageKey = page.PageKey,
        Label = label,
        Description = string.IsNullOrWhiteSpace(page.Description) ? null : page.Description
    };

    public static CheckState StateOf(PermissionNode node, ISet<string> selected)
    {
        var keys = node.LeafKeys;
        if (keys.Count == 0) return CheckState.None;

        var granted = keys.Count(selected.Contains);
        if (granted == 0) return CheckState.None;
        return granted == keys.Count ? CheckState.All : CheckState.Some;
    }

    /// <summary>Ticking a parent grants everything under it; unticking revokes everything under it.</summary>
    public static void Toggle(PermissionNode node, ISet<string> selected, bool isChecked)
    {
        foreach (var key in node.LeafKeys)
        {
            if (isChecked) selected.Add(key);
            else selected.Remove(key);
        }
    }

    /// <summary>
    /// Narrows the tree to what matches <paramref name="term"/>. A leaf matches on its own text or its
    /// ancestors' (searching "appointments" shows every appointment permission); a branch is kept when
    /// anything beneath it is.
    /// </summary>
    public static List<PermissionNode> Filter(IEnumerable<PermissionNode> roots, string? term)
    {
        if (string.IsNullOrWhiteSpace(term)) return roots.ToList();

        var needle = term.Trim();
        var result = new List<PermissionNode>();
        foreach (var root in roots)
        {
            var kept = FilterNode(root, needle, ancestorMatched: false);
            if (kept != null) result.Add(kept);
        }
        return result;
    }

    private static PermissionNode? FilterNode(PermissionNode node, string term, bool ancestorMatched)
    {
        var selfMatches = Matches(node.Label, term)
                          || Matches(node.Description, term)
                          || (node.PageKey != null && Matches(node.PageKey, term));
        var matched = ancestorMatched || selfMatches;

        if (node.IsLeaf) return matched ? node : null;

        var kept = node.Children
            .Select(c => FilterNode(c, term, matched))
            .Where(c => c != null)
            .Cast<PermissionNode>()
            .ToList();
        if (kept.Count == 0) return null;

        return new PermissionNode
        {
            Id = node.Id,
            Label = node.Label,
            Description = node.Description,
            Children = kept,
            LeafKeys = node.LeafKeys // the full set, not just what survived the filter
        };
    }

    private static bool Matches(string? text, string term) =>
        text != null && text.Contains(term, StringComparison.OrdinalIgnoreCase);
}
