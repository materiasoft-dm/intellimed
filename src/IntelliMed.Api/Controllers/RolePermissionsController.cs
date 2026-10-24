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
    [Authorize(Roles = "SuperAdmin,Admin")]
    [ProducesResponseType(typeof(IEnumerable<PageDefinitionDto>), StatusCodes.Status200OK)]
    public ActionResult<IEnumerable<PageDefinitionDto>> GetPageDefinitions()
    {
        return Ok(GetPageDefinitionsList());
    }

    // =========================================================================
    // ROLE PERMISSIONS — CRUD for which pages a role can access
    // =========================================================================

    /// <summary>
    /// Get all role permissions (all role→page mappings).
    /// </summary>
    [HttpGet]
    [Authorize(Roles = "SuperAdmin,Admin")]
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
    [Authorize(Roles = "SuperAdmin,Admin")]
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
    [Authorize(Roles = "SuperAdmin,Admin")]
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
    [Authorize]
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
            var allPages = GetPageDefinitionsList();
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
            "clinic-settings" or "clinic-manager" => "Practice",
            string p when p.StartsWith("clients") || p.StartsWith("appointments") || p.StartsWith("prescriptions") || p.StartsWith("progress-notes") || p.StartsWith("health-issues") => "Clinical",
            string p when p.StartsWith("invoices") || p.StartsWith("payments") || p == "fee-schedules" || p == "derived-item-configs" => "Financial",
            string p when p.StartsWith("admin") => "Admin",
            string p when p.StartsWith("reports") => "Reports",
            _ => "Other"
        };
    }

    /// <summary>The permission catalogue — every page key that can be granted to a role, with the
    /// parent group and action label Role Configuration uses to lay them out as a tree. Public so the
    /// tests can check the catalogue itself.</summary>
    public static List<PageDefinitionDto> GetPageCatalogue() => GetPageDefinitionsList();

    /// <summary>
    /// Returns the page definitions list directly (not wrapped in ActionResult).
    /// Used internally by GetMyAccessiblePages to avoid ActionResult.Value null issue.
    ///
    /// Group/Action only affect how Role Configuration lays the permissions out: a group becomes a
    /// parent checkbox (ticked when all of its permissions are, partly ticked when some are). The stored
    /// and enforced permission is still the flat PageKey. Declaration order is display order.
    /// </summary>
    private static List<PageDefinitionDto> GetPageDefinitionsList()
    {
        static PageDefinitionDto P(string key, string name, string category, string description, string? group = null, string? action = null) =>
            new() { PageKey = key, PageName = name, Category = category, Description = description, Group = group, Action = action };

        return new List<PageDefinitionDto>
        {
            // Clinical
            P("clients", "Client Records", "Clinical", "View and manage client records", "Clients", "View"),
            P("clients/create", "Add Client", "Clinical", "Create new client records", "Clients", "Create"),
            P("clients/edit", "Edit Client", "Clinical", "Edit existing client records", "Clients", "Update"),
            P("clients/delete", "Delete Client", "Clinical", "Remove client records", "Clients", "Remove"),
            P("address-book", "Address Book", "Clinical", "Add and edit GPs, specialists and other providers in the address book"),
            P("results", "Results Inbox", "Clinical", "Review pathology and imaging results received from labs"),
            P("appointments", "Appointments", "Clinical", "View appointment schedule", "Appointments", "View"),
            P("appointments/create", "New Appointment", "Clinical", "Schedule new appointments", "Appointments", "Create"),
            P("appointments/edit", "Edit Appointment", "Clinical", "Modify existing appointments", "Appointments", "Update"),
            P("appointments/delete", "Delete Appointment", "Clinical", "Cancel appointments", "Appointments", "Remove"),
            P("appointments/waiting-room", "Waiting Room", "Clinical", "View and manage the waiting room", "Appointments", "Waiting room"),
            P("prescriptions", "Prescriptions", "Clinical", "View a client's prescriptions", "Prescriptions", "View"),
            P("prescriptions/create", "Write Prescription", "Clinical", "Write new prescriptions and run the safety check", "Prescriptions", "Create"),
            P("prescriptions/edit", "Edit Prescription", "Clinical", "Update, cease, cancel, and issue/reprint existing prescriptions", "Prescriptions", "Update"),
            P("progress-notes", "Progress Notes", "Clinical", "View a client's progress notes and episodes", "Progress Notes", "View"),
            P("progress-notes/create", "Write Progress Note", "Clinical", "Create new progress notes and episodes", "Progress Notes", "Create"),
            P("progress-notes/edit", "Edit Progress Note", "Clinical", "Update drafts, finalize, add addenda, delete drafts, and manage episodes", "Progress Notes", "Update"),
            P("health-issues", "Health Issues", "Clinical", "View a patient's problem list", "Health Issues", "View"),
            P("health-issues/create", "Add Health Issue", "Clinical", "Record a new condition on the problem list", "Health Issues", "Create"),
            P("health-issues/edit", "Edit Health Issue", "Clinical", "Update or archive an existing health issue", "Health Issues", "Update"),

            // Financial
            P("invoices", "Invoices", "Financial", "View invoices", "Invoices", "View"),
            P("invoices/create", "New Invoice", "Financial", "Create new invoices", "Invoices", "Create"),
            P("invoices/edit", "Edit Invoice", "Financial", "Modify existing invoices", "Invoices", "Update"),
            P("invoices/delete", "Delete Invoice", "Financial", "Remove invoices", "Invoices", "Remove"),
            P("invoices/refund", "Refund Invoice", "Financial", "Refund a paid invoice or a payer's account credit", "Invoices", "Refund"),
            P("invoices/write-off", "Write Off Invoice", "Financial", "Forgive part or all of an invoice's outstanding balance", "Invoices", "Write off"),
            P("invoices/void", "Cancel Invoice", "Financial", "Cancel an unpaid invoice", "Invoices", "Cancel"),
            P("invoices/split", "Split Invoice", "Financial", "Divide an unpaid invoice's line items across new invoices", "Invoices", "Split"),
            P("payments", "Payments", "Financial", "View and process payments"),
            P("fee-schedules", "Fee Schedules", "Financial", "Manage fee schedules and health fund rate tables"),
            P("derived-item-configs", "Derived Item Rules", "Financial", "Configure MBS items whose fee derives from other items on the invoice"),

            // Admin
            P("admin/users", "User Management", "Admin", "Manage system users"),
            P("admin/roles", "Role Configuration", "Admin", "Configure role permissions"),
            P("admin/settings", "System Settings", "Admin", "Configure system parameters"),
            P("admin/appointment-types", "Appointment Types", "Admin", "Configure the appointment-type/duration-preset catalogue"),
            P("admin/email-templates", "Email Templates", "Admin", "Author and assign email templates to system events (invite, forgot password)"),
            P("admin/search-actions", "Command Palette Actions", "Admin", "Manage the entries searchable from the global command palette"),
            P("admin/database-backups", "Database Backups", "Admin", "Configure and manage scheduled SQLite database backups"),
            P("admin/medicines", "Medicine Catalog", "Admin", "Manage the medicines reference-data catalog and its CSV import"),
            P("admin/drug-interactions", "Drug Interactions", "Admin", "Manage the drug-interaction reference table used by the prescribing safety check"),

            // Practice
            P("clinic-settings", "Clinic Settings", "Practice", "Configure practice-wide identity and contact information"),
            P("clinic-manager", "Clinic Manager", "Practice", "Manage clinic locations and which users belong to each"),

            // Reports
            P("reports", "Reports Dashboard", "Reports", "View practice reports", "Reports", "Dashboard"),
            P("reports/financial", "Financial Reports", "Reports", "Revenue and billing reports", "Reports", "Financial"),
            P("reports/clinical", "Clinical Reports", "Reports", "Client and appointment analytics", "Reports", "Clinical"),
        };
    }
}
