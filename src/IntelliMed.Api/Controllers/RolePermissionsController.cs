using IntelliMed.Core.DTOs;
using IntelliMed.Core.Entities;
using IntelliMed.Infrastructure.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace IntelliMed.Api.Controllers;

/// <summary>
/// Controller for managing dynamic role-to-page permissions.
/// SuperAdmin and Admin can configure which pages each role can access.
/// </summary>
[ApiController]
[Route("api/admin/role-permissions")]
[Authorize(Roles = "SuperAdmin,Admin")]
public class RolePermissionsController : ControllerBase
{
    private readonly AppDbContext _context;
    private readonly ILogger<RolePermissionsController> _logger;

    public RolePermissionsController(AppDbContext context, ILogger<RolePermissionsController> logger)
    {
        _context = context;
        _logger = logger;
    }

    // =========================================================================
    // PAGE DEFINITIONS — the catalog of all available pages
    // =========================================================================

    /// <summary>
    /// Get all available page definitions (the catalog of pages that can be assigned to roles).
    /// </summary>
    [HttpGet("page-definitions")]
    [ProducesResponseType(typeof(IEnumerable<PageDefinitionDto>), StatusCodes.Status200OK)]
    public ActionResult<IEnumerable<PageDefinitionDto>> GetPageDefinitions()
    {
        var pages = new List<PageDefinitionDto>
        {
            // Clinical
            new() { PageKey = "patients", PageName = "Patient Records", Category = "Clinical", Description = "View and manage patient records" },
            new() { PageKey = "patients/create", PageName = "Add Patient", Category = "Clinical", Description = "Create new patient records" },
            new() { PageKey = "patients/edit", PageName = "Edit Patient", Category = "Clinical", Description = "Edit existing patient records" },
            new() { PageKey = "patients/delete", PageName = "Delete Patient", Category = "Clinical", Description = "Remove patient records" },
            new() { PageKey = "appointments", PageName = "Appointments", Category = "Clinical", Description = "View appointment schedule" },
            new() { PageKey = "appointments/create", PageName = "New Appointment", Category = "Clinical", Description = "Schedule new appointments" },
            new() { PageKey = "appointments/edit", PageName = "Edit Appointment", Category = "Clinical", Description = "Modify existing appointments" },
            new() { PageKey = "appointments/delete", PageName = "Delete Appointment", Category = "Clinical", Description = "Cancel appointments" },
            new() { PageKey = "practitioners", PageName = "Practitioners", Category = "Clinical", Description = "View practitioner directory" },
            new() { PageKey = "practitioners/create", PageName = "Add Practitioner", Category = "Clinical", Description = "Register new practitioners" },
            new() { PageKey = "practitioners/edit", PageName = "Edit Practitioner", Category = "Clinical", Description = "Update practitioner details" },

            // Financial
            new() { PageKey = "invoices", PageName = "Invoices", Category = "Financial", Description = "View invoices" },
            new() { PageKey = "invoices/create", PageName = "New Invoice", Category = "Financial", Description = "Create new invoices" },
            new() { PageKey = "invoices/edit", PageName = "Edit Invoice", Category = "Financial", Description = "Modify existing invoices" },
            new() { PageKey = "invoices/delete", PageName = "Delete Invoice", Category = "Financial", Description = "Remove invoices" },
            new() { PageKey = "payments", PageName = "Payments", Category = "Financial", Description = "View and process payments" },

            // Admin
            new() { PageKey = "admin/users", PageName = "User Management", Category = "Admin", Description = "Manage system users" },
            new() { PageKey = "admin/roles", PageName = "Role Configuration", Category = "Admin", Description = "Configure role permissions" },
            new() { PageKey = "admin/audit", PageName = "Audit Log", Category = "Admin", Description = "View system audit trail" },
            new() { PageKey = "admin/settings", PageName = "System Settings", Category = "Admin", Description = "Configure system parameters" },

            // Reports
            new() { PageKey = "reports", PageName = "Reports Dashboard", Category = "Reports", Description = "View practice reports" },
            new() { PageKey = "reports/financial", PageName = "Financial Reports", Category = "Reports", Description = "Revenue and billing reports" },
            new() { PageKey = "reports/clinical", PageName = "Clinical Reports", Category = "Reports", Description = "Patient and appointment analytics" },
        };

        return Ok(pages);
    }

    // =========================================================================
    // ROLE PERMISSIONS — CRUD for which pages a role can access
    // =========================================================================

    /// <summary>
    /// Get all role permissions (all role→page mappings).
    /// </summary>
    [HttpGet]
    [ProducesResponseType(typeof(IEnumerable<RolePermissionsDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IEnumerable<RolePermissionsDto>>> GetAllRolePermissions()
    {
        var permissions = await _context.RolePermissions.ToListAsync();

        var grouped = permissions
            .GroupBy(p => p.RoleName)
            .Select(g => new RolePermissionsDto
            {
                RoleName = g.Key,
                PageKeys = g.Select(p => p.PageKey).ToList()
            });

        return Ok(grouped);
    }

    /// <summary>
    /// Get permissions for a specific role.
    /// </summary>
    [HttpGet("{roleName}")]
    [ProducesResponseType(typeof(RolePermissionsDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<RolePermissionsDto>> GetRolePermissions(string roleName)
    {
        var pageKeys = await _context.RolePermissions
            .Where(p => p.RoleName == roleName)
            .Select(p => p.PageKey)
            .ToListAsync();

        return Ok(new RolePermissionsDto
        {
            RoleName = roleName,
            PageKeys = pageKeys
        });
    }

    /// <summary>
    /// Save (replace) all page permissions for a role.
    /// </summary>
    [HttpPut("{roleName}")]
    [ProducesResponseType(typeof(UserManagementResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(UserManagementResponse), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<UserManagementResponse>> SaveRolePermissions(
        string roleName, [FromBody] SaveRolePermissionsRequest request)
    {
        // Remove existing permissions for this role
        var existing = await _context.RolePermissions
            .Where(p => p.RoleName == roleName)
            .ToListAsync();

        _context.RolePermissions.RemoveRange(existing);

        // Add new permissions
        var newPermissions = request.PageKeys.Select(pk => new RolePermission
        {
            RoleName = roleName,
            PageKey = pk,
            Category = GetCategoryForPage(pk)
        }).ToList();

        await _context.RolePermissions.AddRangeAsync(newPermissions);
        await _context.SaveChangesAsync();

        _logger.LogInformation("Permissions updated for role '{Role}': {Pages}",
            roleName, string.Join(", ", request.PageKeys));

        return Ok(new UserManagementResponse
        {
            Success = true,
            Message = $"Permissions saved for role '{roleName}'."
        });
    }

    /// <summary>
    /// Get the pages accessible by the current user (based on their roles).
    /// Used by the frontend to determine which nav items to show.
    /// </summary>
    [HttpGet("my-pages")]
    [ProducesResponseType(typeof(IEnumerable<string>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IEnumerable<string>>> GetMyAccessiblePages()
    {
        var roles = User.Claims
            .Where(c => c.Type == System.Security.Claims.ClaimTypes.Role)
            .Select(c => c.Value)
            .ToList();

        // SuperAdmin always gets all pages
        if (roles.Contains("SuperAdmin"))
        {
            var allPages = GetPageDefinitions().Value!;
            return Ok(allPages.Select(p => p.PageKey).ToList());
        }

        var pageKeys = await _context.RolePermissions
            .Where(p => roles.Contains(p.RoleName))
            .Select(p => p.PageKey)
            .Distinct()
            .ToListAsync();

        return Ok(pageKeys);
    }

    // =========================================================================
    // HELPERS
    // =========================================================================

    private static string GetCategoryForPage(string pageKey)
    {
        return pageKey switch
        {
            string p when p.StartsWith("patients") || p.StartsWith("appointments") || p.StartsWith("practitioners") => "Clinical",
            string p when p.StartsWith("invoices") || p.StartsWith("payments") => "Financial",
            string p when p.StartsWith("admin") => "Admin",
            string p when p.StartsWith("reports") => "Reports",
            _ => "Other"
        };
    }
}