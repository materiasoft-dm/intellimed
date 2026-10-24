using System.Security.Claims;
using IntelliMed.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace IntelliMed.Api.Authorization;

/// <summary>
/// The single implementation of "does this caller's role grant access to this page key?", checked
/// against the RolePermissions catalog.
///
/// This previously existed as nine byte-identical private copies across controllers — the most
/// security-sensitive logic in the API, duplicated the most times and covered by no tests at all.
/// Nine copies means nine places to miss when the rule changes (e.g. adding a second always-allowed
/// role); one copy means one place to change and one place to test.
/// </summary>
public static class RolePermissions
{
    /// <summary>Bypasses the catalog entirely — SuperAdmin is intentionally all-access, so a newly
    /// added page is reachable by an admin before its RolePermissions rows are seeded.</summary>
    public const string AllAccessRole = "SuperAdmin";

    public static IReadOnlyList<string> RolesOf(ClaimsPrincipal user) =>
        user.Claims
            .Where(c => c.Type == ClaimTypes.Role)
            .Select(c => c.Value)
            .ToList();

    /// <summary>True when the caller's role holds at least one of <paramref name="pageKeys"/>.</summary>
    public static async Task<bool> HasAnyAccessAsync(AppDbContext context, ClaimsPrincipal user, IReadOnlyCollection<string> pageKeys)
    {
        var roles = RolesOf(user);
        if (roles.Contains(AllAccessRole)) return true;
        if (roles.Count == 0 || pageKeys.Count == 0) return false;

        var keys = pageKeys.ToList();
        return await context.RolePermissions
            .AnyAsync(p => roles.Contains(p.RoleName) && keys.Contains(p.PageKey));
    }

    public static async Task<bool> HasAccessAsync(AppDbContext context, ClaimsPrincipal user, string pageKey)
    {
        var roles = RolesOf(user);

        if (roles.Contains(AllAccessRole)) return true;

        // No roles at all can never match a catalog row, but short-circuiting keeps a pointless
        // query off the hot path for unauthenticated/role-less principals.
        if (roles.Count == 0) return false;

        return await context.RolePermissions
            .AnyAsync(p => roles.Contains(p.RoleName) && p.PageKey == pageKey);
    }
}
