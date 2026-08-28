using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace IntelliMed.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddDrugInteractionAdminRolePermission : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Grants the new admin/drug-interactions page to SuperAdmin and Admin. Needed as a
            // migration (not just the Program.cs code-seed) because SeedRolePermissionsAsync no-ops
            // once the RolePermissions table has any rows at all — true on every already-deployed
            // database. Mirrors AddMedicineAdminRolePermission.cs exactly.
            migrationBuilder.InsertData(
                table: "RolePermissions",
                columns: new[] { "RoleName", "PageKey", "Category" },
                values: new object[,]
                {
                    { "SuperAdmin", "admin/drug-interactions", "Admin" },
                    { "Admin", "admin/drug-interactions", "Admin" }
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
                    { "SuperAdmin", "admin/drug-interactions" },
                    { "Admin", "admin/drug-interactions" }
                });
        }
    }
}
