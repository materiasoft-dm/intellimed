using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace IntelliMed.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddPrescriptionRolePermissions : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Grants the new prescriptions/prescriptions-create/prescriptions-edit pages. Needed as a
            // migration (not just the Program.cs code-seed) because SeedRolePermissionsAsync no-ops
            // once the RolePermissions table has any rows at all — true on every already-deployed
            // database. SuperAdmin/Doctor get full access; Nurse/Admin view-only (Admin deliberately
            // excluded from create/edit — see the seed comment in Program.cs); Receptionist gets none.
            migrationBuilder.InsertData(
                table: "RolePermissions",
                columns: new[] { "RoleName", "PageKey", "Category" },
                values: new object[,]
                {
                    { "SuperAdmin", "prescriptions", "Clinical" },
                    { "SuperAdmin", "prescriptions/create", "Clinical" },
                    { "SuperAdmin", "prescriptions/edit", "Clinical" },
                    { "Doctor", "prescriptions", "Clinical" },
                    { "Doctor", "prescriptions/create", "Clinical" },
                    { "Doctor", "prescriptions/edit", "Clinical" },
                    { "Nurse", "prescriptions", "Clinical" },
                    { "Admin", "prescriptions", "Clinical" }
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
                    { "SuperAdmin", "prescriptions" },
                    { "SuperAdmin", "prescriptions/create" },
                    { "SuperAdmin", "prescriptions/edit" },
                    { "Doctor", "prescriptions" },
                    { "Doctor", "prescriptions/create" },
                    { "Doctor", "prescriptions/edit" },
                    { "Nurse", "prescriptions" },
                    { "Admin", "prescriptions" }
                });
        }
    }
}
