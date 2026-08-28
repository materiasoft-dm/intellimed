using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace IntelliMed.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddProgressNotesRolePermissions : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Grants the new progress-notes/progress-notes-create/progress-notes-edit pages. Needed as a
            // migration (not just the Program.cs code-seed) because SeedRolePermissionsAsync no-ops
            // once the RolePermissions table has any rows at all — true on every already-deployed
            // database. SuperAdmin/Doctor/Nurse get full access (Nurse deliberately full here, unlike
            // prescriptions — see the seed comment in Program.cs); Admin view-only; Receptionist none.
            migrationBuilder.InsertData(
                table: "RolePermissions",
                columns: new[] { "RoleName", "PageKey", "Category" },
                values: new object[,]
                {
                    { "SuperAdmin", "progress-notes", "Clinical" },
                    { "SuperAdmin", "progress-notes/create", "Clinical" },
                    { "SuperAdmin", "progress-notes/edit", "Clinical" },
                    { "Doctor", "progress-notes", "Clinical" },
                    { "Doctor", "progress-notes/create", "Clinical" },
                    { "Doctor", "progress-notes/edit", "Clinical" },
                    { "Nurse", "progress-notes", "Clinical" },
                    { "Nurse", "progress-notes/create", "Clinical" },
                    { "Nurse", "progress-notes/edit", "Clinical" },
                    { "Admin", "progress-notes", "Clinical" }
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
                    { "SuperAdmin", "progress-notes" },
                    { "SuperAdmin", "progress-notes/create" },
                    { "SuperAdmin", "progress-notes/edit" },
                    { "Doctor", "progress-notes" },
                    { "Doctor", "progress-notes/create" },
                    { "Doctor", "progress-notes/edit" },
                    { "Nurse", "progress-notes" },
                    { "Nurse", "progress-notes/create" },
                    { "Nurse", "progress-notes/edit" },
                    { "Admin", "progress-notes" }
                });
        }
    }
}
