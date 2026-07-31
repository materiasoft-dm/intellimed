using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace IntelliMed.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddSearchActions : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "SearchActions",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Title = table.Column<string>(type: "TEXT", maxLength: 150, nullable: false),
                    Keywords = table.Column<string>(type: "TEXT", maxLength: 500, nullable: true),
                    Description = table.Column<string>(type: "TEXT", maxLength: 255, nullable: true),
                    Category = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    ActionType = table.Column<int>(type: "INTEGER", nullable: false),
                    Target = table.Column<string>(type: "TEXT", maxLength: 255, nullable: false),
                    PageKey = table.Column<string>(type: "TEXT", maxLength: 200, nullable: true),
                    SortOrder = table.Column<int>(type: "INTEGER", nullable: false),
                    IsActive = table.Column<bool>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SearchActions", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_SearchActions_IsActive",
                table: "SearchActions",
                column: "IsActive");

            // Seed the initial command-palette catalogue — one row per currently-routable page.
            // ActionType 0 = Navigate for all of these. PageKey ties each entry to the existing
            // RolePermissions catalogue so the palette can filter by what the caller can access.
            migrationBuilder.InsertData(
                table: "SearchActions",
                columns: new[] { "Title", "Keywords", "Description", "Category", "ActionType", "Target", "PageKey", "SortOrder", "IsActive" },
                values: new object[,]
                {
                    { "Dashboard", "home overview", "Practice overview and quick links", "General", 0, "/", null, 0, true },
                    { "Find Client", "patient search view people", "Search and manage client records", "Clinical", 0, "/clients/search", "clients", 10, true },
                    { "Add Client", "create new patient register", "Create a new client record", "Clinical", 0, "/clients/add", "clients/create", 11, true },
                    { "Appointment List", "schedule bookings", "View the appointment schedule", "Clinical", 0, "/appointments", "appointments", 20, true },
                    { "New Appointment", "create new book", "Schedule a new appointment", "Clinical", 0, "/appointments/new", "appointments/create", 21, true },
                    { "Calendar View", "schedule calendar", "Appointment calendar view", "Clinical", 0, "/appointments/calendar", "appointments", 22, true },
                    { "Waiting Room", "queue checkin", "View and manage the waiting room", "Clinical", 0, "/appointments/waiting-room", "appointments/waiting-room", 23, true },
                    { "Invoice List", "billing", "View invoices", "Financial", 0, "/invoices", "invoices", 30, true },
                    { "New Invoice", "create new bill", "Create a new invoice", "Financial", 0, "/invoices/new", "invoices/create", 31, true },
                    { "Payments", "billing pay", "View and process payments", "Financial", 0, "/invoices/payments", "payments", 32, true },
                    { "Fee Schedules", "pricing rates", "Manage fee schedules and rate tables", "Financial", 0, "/fee-schedules", "fee-schedules", 33, true },
                    { "Derived Item Rules", "mbs pricing rules", "Configure derived-fee item rules", "Financial", 0, "/derived-item-configs", "derived-item-configs", 34, true },
                    { "User Management", "staff accounts invite", "Manage system users", "Admin", 0, "/admin/users", "admin/users", 40, true },
                    { "Role Configuration", "permissions access", "Configure role page access", "Admin", 0, "/admin/roles", "admin/roles", 41, true },
                    { "Appointment Types", "duration presets", "Configure appointment type/duration presets", "Admin", 0, "/admin/appointment-types", "admin/appointment-types", 42, true },
                    { "Email Templates", "invite reset mail", "Author and assign email templates", "Admin", 0, "/admin/email-templates", "admin/email-templates", 43, true },
                    { "Clinic Settings", "smtp email practice", "Configure practice information and email", "Practice", 0, "/clinic-settings", "clinic-settings", 50, true },
                    { "Clinic Manager", "locations branches", "Manage clinic locations and staff", "Practice", 0, "/clinic-manager", "clinic-manager", 51, true },
                    { "Profile Settings", "account password schedule", "Your own profile and weekly schedule", "General", 0, "/profile", null, 60, true }
                });

            // Grants the new "admin/search-actions" page to SuperAdmin/Admin on existing databases
            // (Program.cs's SeedRolePermissionsAsync only runs against an empty RolePermissions table).
            migrationBuilder.InsertData(
                table: "RolePermissions",
                columns: new[] { "RoleName", "PageKey", "Category" },
                values: new object[,]
                {
                    { "SuperAdmin", "admin/search-actions", "Admin" },
                    { "Admin", "admin/search-actions", "Admin" }
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DeleteData(
                table: "RolePermissions",
                keyColumns: new[] { "RoleName", "PageKey" },
                keyValues: new object[,]
                {
                    { "SuperAdmin", "admin/search-actions" },
                    { "Admin", "admin/search-actions" }
                });

            migrationBuilder.DropTable(
                name: "SearchActions");
        }
    }
}
