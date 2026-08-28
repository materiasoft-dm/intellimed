using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace IntelliMed.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddHealthIssueRolePermissions : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Grants the new health-issues pages. Needed as a migration (not just the Program.cs
            // code-seed) because SeedRolePermissionsAsync no-ops once the RolePermissions table has
            // any rows at all — true on every already-deployed database. Grants mirror
            // progress-notes exactly: SuperAdmin/Doctor/Nurse full, Admin view-only, Receptionist
            // none — a problem list is a clinical record, not front-desk data.
            migrationBuilder.InsertData(
                table: "RolePermissions",
                columns: new[] { "RoleName", "PageKey", "Category" },
                values: new object[,]
                {
                    { "SuperAdmin", "health-issues", "Clinical" },
                    { "SuperAdmin", "health-issues/create", "Clinical" },
                    { "SuperAdmin", "health-issues/edit", "Clinical" },
                    { "Doctor", "health-issues", "Clinical" },
                    { "Doctor", "health-issues/create", "Clinical" },
                    { "Doctor", "health-issues/edit", "Clinical" },
                    { "Nurse", "health-issues", "Clinical" },
                    { "Nurse", "health-issues/create", "Clinical" },
                    { "Nurse", "health-issues/edit", "Clinical" },
                    { "Admin", "health-issues", "Clinical" }
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            foreach (var role in new[] { "SuperAdmin", "Doctor", "Nurse" })
            {
                foreach (var page in new[] { "health-issues", "health-issues/create", "health-issues/edit" })
                {
                    migrationBuilder.DeleteData(
                        table: "RolePermissions",
                        keyColumns: new[] { "RoleName", "PageKey" },
                        keyValues: new object[] { role, page });
                }
            }

            migrationBuilder.DeleteData(
                table: "RolePermissions",
                keyColumns: new[] { "RoleName", "PageKey" },
                keyValues: new object[] { "Admin", "health-issues" });
        }
    }
}
