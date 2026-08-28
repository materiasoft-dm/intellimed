using System.Security.Claims;
using FluentAssertions;
using IntelliMed.Api.Authorization;
using IntelliMed.Core.Entities;
using IntelliMed.Infrastructure.Data;
using IntelliMed.Tests.Helpers;
using Xunit;

namespace IntelliMed.Tests.Authorization;

/// <summary>
/// Covers the shared RBAC check that nine controllers now delegate to. Before it was extracted this
/// logic existed as nine identical private copies with no tests at all, despite being the gate in
/// front of every client-scoped clinical endpoint.
/// </summary>
public class RolePermissionsTests : IDisposable
{
    private readonly AppDbContext _context;

    public RolePermissionsTests()
    {
        _context = TestDbContextFactory.CreateInMemoryContext();
    }

    public void Dispose() => _context.Dispose();

    private static ClaimsPrincipal UserWithRoles(params string[] roles) =>
        new(new ClaimsIdentity(roles.Select(r => new Claim(ClaimTypes.Role, r)), "TestAuth"));

    private async Task GrantAsync(string roleName, string pageKey)
    {
        _context.RolePermissions.Add(new RolePermission { RoleName = roleName, PageKey = pageKey });
        await _context.SaveChangesAsync();
    }

    [Fact]
    public async Task SuperAdmin_IsGranted_EvenWithNoCatalogRows()
    {
        var result = await RolePermissions.HasAccessAsync(_context, UserWithRoles("SuperAdmin"), "prescriptions");

        result.Should().BeTrue();
    }

    [Fact]
    public async Task RoleWithMatchingCatalogRow_IsGranted()
    {
        await GrantAsync("Doctor", "prescriptions");

        var result = await RolePermissions.HasAccessAsync(_context, UserWithRoles("Doctor"), "prescriptions");

        result.Should().BeTrue();
    }

    [Fact]
    public async Task RoleWithoutMatchingCatalogRow_IsDenied()
    {
        await GrantAsync("Doctor", "prescriptions");

        var result = await RolePermissions.HasAccessAsync(_context, UserWithRoles("Receptionist"), "prescriptions");

        result.Should().BeFalse();
    }

    [Fact]
    public async Task GrantIsScopedToItsOwnPageKey_NotAllPages()
    {
        await GrantAsync("Doctor", "prescriptions");

        var otherPage = await RolePermissions.HasAccessAsync(_context, UserWithRoles("Doctor"), "prescriptions/create");

        otherPage.Should().BeFalse("a grant on one page key must not leak into a different one");
    }

    [Fact]
    public async Task UserWithNoRoles_IsDenied()
    {
        await GrantAsync("Doctor", "prescriptions");

        var result = await RolePermissions.HasAccessAsync(_context, UserWithRoles(), "prescriptions");

        result.Should().BeFalse();
    }

    [Fact]
    public async Task AnyOneOfSeveralRolesMatching_IsEnough()
    {
        await GrantAsync("Nurse", "progress-notes");

        var result = await RolePermissions.HasAccessAsync(
            _context, UserWithRoles("Receptionist", "Nurse"), "progress-notes");

        result.Should().BeTrue();
    }

    [Fact]
    public async Task EmptyCatalog_DeniesEveryNonSuperAdminRole()
    {
        var result = await RolePermissions.HasAccessAsync(_context, UserWithRoles("Doctor"), "prescriptions");

        result.Should().BeFalse("an unseeded catalog must fail closed, not open");
    }

    [Fact]
    public void RolesOf_ReadsOnlyRoleClaims()
    {
        var principal = new ClaimsPrincipal(new ClaimsIdentity(new[]
        {
            new Claim(ClaimTypes.Role, "Doctor"),
            new Claim(ClaimTypes.Name, "not-a-role"),
            new Claim(ClaimTypes.Role, "Nurse")
        }, "TestAuth"));

        RolePermissions.RolesOf(principal).Should().BeEquivalentTo("Doctor", "Nurse");
    }
}
