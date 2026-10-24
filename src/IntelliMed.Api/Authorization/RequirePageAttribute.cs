using IntelliMed.Infrastructure.Data;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

namespace IntelliMed.Api.Authorization;

/// <summary>
/// Requires the caller's role to hold a page key (see <see cref="RolePermissions"/>), so the API
/// enforces what Role Configuration says rather than leaving it to the sidebar hiding a link.
///
/// Give it several keys separated by <c>|</c> and any one of them is enough — for endpoints that are
/// shared by more than one feature (looking a client up is needed to book an appointment, raise an
/// invoice or match a result, so it is not the exclusive property of <c>clients</c>). Use the constants
/// in <see cref="PageAccess"/> for the recurring sets.
///
/// Put it on an action, or on a class to cover every action in it. Several attributes on one action
/// must ALL pass. SuperAdmin always passes, as everywhere else. An unauthenticated caller never reaches
/// this — the global fallback policy rejects them with 401 first.
/// </summary>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method, AllowMultiple = true)]
public sealed class RequirePageAttribute : Attribute, IAsyncAuthorizationFilter
{
    public const char Separator = '|';

    public RequirePageAttribute(string keys)
    {
        Keys = keys.Split(Separator, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (Keys.Count == 0)
            throw new ArgumentException("At least one page key is required.", nameof(keys));
    }

    public IReadOnlyList<string> Keys { get; }

    public async Task OnAuthorizationAsync(AuthorizationFilterContext context)
    {
        var db = context.HttpContext.RequestServices.GetRequiredService<AppDbContext>();

        if (!await RolePermissions.HasAnyAccessAsync(db, context.HttpContext.User, Keys))
        {
            context.Result = new ForbidResult();
        }
    }
}
