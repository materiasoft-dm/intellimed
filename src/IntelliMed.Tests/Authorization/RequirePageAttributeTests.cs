using System.Security.Claims;
using FluentAssertions;
using IntelliMed.Api.Authorization;
using IntelliMed.Core.Entities;
using IntelliMed.Infrastructure.Data;
using IntelliMed.Tests.Helpers;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Abstractions;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace IntelliMed.Tests.Authorization;

/// <summary>
/// The [RequirePage] filter is what makes a role's page keys bind the API instead of merely hiding
/// sidebar links, so its rules are pinned down here: any one key suffices, SuperAdmin always passes,
/// and everyone else needs a matching row in the permission catalogue.
/// </summary>
public class RequirePageAttributeTests : IDisposable
{
    private readonly AppDbContext _context = TestDbContextFactory.CreateInMemoryContext();

    public void Dispose() => _context.Dispose();

    private async Task GrantAsync(string role, string key)
    {
        _context.RolePermissions.Add(new RolePermission { RoleName = role, PageKey = key });
        await _context.SaveChangesAsync();
    }

    private async Task<IActionResult?> RunAsync(RequirePageAttribute attribute, params string[] roles)
    {
        var services = new ServiceCollection().AddSingleton(_context).BuildServiceProvider();
        var http = new DefaultHttpContext
        {
            RequestServices = services,
            User = new ClaimsPrincipal(new ClaimsIdentity(roles.Select(r => new Claim(ClaimTypes.Role, r)), "TestAuth"))
        };
        var context = new AuthorizationFilterContext(
            new ActionContext(http, new RouteData(), new ActionDescriptor()), new List<IFilterMetadata>());

        await attribute.OnAuthorizationAsync(context);
        return context.Result;
    }

    [Fact]
    public async Task RoleHoldingTheKey_Passes()
    {
        await GrantAsync("Doctor", "clients/edit");

        (await RunAsync(new RequirePageAttribute("clients/edit"), "Doctor")).Should().BeNull();
    }

    [Fact]
    public async Task RoleWithoutTheKey_IsForbidden()
    {
        await GrantAsync("Doctor", "clients");

        (await RunAsync(new RequirePageAttribute("clients/delete"), "Doctor")).Should().BeOfType<ForbidResult>();
    }

    [Fact]
    public async Task AnyOneOfSeveralKeys_IsEnough()
    {
        await GrantAsync("Nurse", "results");

        (await RunAsync(new RequirePageAttribute("clients|results|invoices"), "Nurse")).Should().BeNull();
    }

    [Fact]
    public async Task NoneOfSeveralKeys_IsForbidden()
    {
        await GrantAsync("Nurse", "address-book");

        (await RunAsync(new RequirePageAttribute("clients|results|invoices"), "Nurse")).Should().BeOfType<ForbidResult>();
    }

    [Fact]
    public async Task SuperAdmin_AlwaysPasses_EvenWithNoRows()
    {
        (await RunAsync(new RequirePageAttribute("fee-schedules"), "SuperAdmin")).Should().BeNull();
    }

    [Fact]
    public async Task ACallerWithNoRoles_IsForbidden()
    {
        await GrantAsync("Doctor", "clients");

        (await RunAsync(new RequirePageAttribute("clients"))).Should().BeOfType<ForbidResult>();
    }

    [Fact]
    public async Task AKeyGrantedToADifferentRole_DoesNotCount()
    {
        await GrantAsync("Receptionist", "fee-schedules");

        (await RunAsync(new RequirePageAttribute("fee-schedules"), "Nurse")).Should().BeOfType<ForbidResult>();
    }

    [Fact]
    public async Task ARolesFirstMatchingGrantIsEnough_WhenTheCallerHoldsSeveralRoles()
    {
        await GrantAsync("Receptionist", "payments");

        (await RunAsync(new RequirePageAttribute("payments"), "Nurse", "Receptionist")).Should().BeNull();
    }

    [Fact]
    public void Keys_AreSplitOnThePipe_AndTrimmed()
    {
        new RequirePageAttribute(" clients | results ").Keys.Should().Equal("clients", "results");
    }

    [Theory]
    [InlineData("")]
    [InlineData(" | ")]
    public void NoKeys_IsRejectedWhenTheAttributeIsBuilt(string keys)
    {
        var act = () => new RequirePageAttribute(keys);

        act.Should().Throw<ArgumentException>();
    }
}
