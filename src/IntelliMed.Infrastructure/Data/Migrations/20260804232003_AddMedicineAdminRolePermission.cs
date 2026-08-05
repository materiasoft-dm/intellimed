using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace IntelliMed.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddMedicineAdminRolePermission : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Grants the new admin/medicines page to SuperAdmin and Admin. Needed as a migration (not
            // just the Program.cs code-seed) because SeedRolePermissionsAsync no-ops once the
            // RolePermissions table has any rows at all — true on every already-deployed database.
            migrationBuilder.InsertData(
                table: "RolePermissions",
                columns: new[] { "RoleName", "PageKey", "Category" },
                values: new object[,]
                {
                    { "SuperAdmin", "admin/medicines", "Admin" },
                    { "Admin", "admin/medicines", "Admin" }
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
                    { "SuperAdmin", "admin/medicines" },
                    { "Admin", "admin/medicines" }
                });
        }
    }
}
