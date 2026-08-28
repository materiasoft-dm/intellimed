using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace IntelliMed.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class RemoveOrphanedPractitionerPageKeys : Migration
    {
        private static readonly string[] OrphanedKeys =
        {
            "practitioners", "practitioners/create", "practitioners/edit",
            // No /admin/audit screen exists and there is no application audit trail to view — the
            // only audit table, DbAdminAuditLog, covers the isolated /dbadmin console alone.
            "admin/audit"
        };

        private static readonly string[] GrantedRoles =
        {
            "SuperAdmin", "Admin", "Doctor", "Nurse", "Receptionist"
        };

        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // These three page keys had no screen and no enforcement anywhere — there is no
            // /practitioners route, and no controller ever checked them. Practitioners are managed
            // through /admin/users, which links a login to a Practitioner record. Leaving them in
            // place meant the Roles screen offered toggles that could never do anything.
            //
            // Removed from the catalogue and the code seed as well; this clears the rows already
            // sitting in deployed databases. Reports keys are deliberately NOT removed — reporting
            // is genuinely planned (see ARCHITECTURE.md "Next Steps"), those are placeholders.
            foreach (var role in GrantedRoles)
            {
                foreach (var key in OrphanedKeys)
                {
                    migrationBuilder.DeleteData(
                        table: "RolePermissions",
                        keyColumns: new[] { "RoleName", "PageKey" },
                        keyValues: new object[] { role, key });
                }
            }
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Restores only the grants the seed originally shipped, not the full cross product.
            migrationBuilder.InsertData(
                table: "RolePermissions",
                columns: new[] { "RoleName", "PageKey", "Category" },
                values: new object[,]
                {
                    { "SuperAdmin", "practitioners", "Clinical" },
                    { "SuperAdmin", "practitioners/create", "Clinical" },
                    { "SuperAdmin", "practitioners/edit", "Clinical" },
                    { "Admin", "practitioners", "Clinical" },
                    { "Admin", "practitioners/create", "Clinical" },
                    { "Admin", "practitioners/edit", "Clinical" },
                    { "Doctor", "practitioners", "Clinical" },
                    { "Nurse", "practitioners", "Clinical" },
                    { "SuperAdmin", "admin/audit", "Admin" },
                    { "Admin", "admin/audit", "Admin" }
                });
        }
    }
}
