using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace IntelliMed.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddDatabaseBackups : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "DatabaseBackups",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    FileName = table.Column<string>(type: "TEXT", maxLength: 255, nullable: false),
                    FileSizeBytes = table.Column<long>(type: "INTEGER", nullable: false),
                    Trigger = table.Column<int>(type: "INTEGER", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DatabaseBackups", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "DatabaseBackupSettings",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    IntervalValue = table.Column<int>(type: "INTEGER", nullable: false),
                    IntervalUnit = table.Column<int>(type: "INTEGER", nullable: false),
                    LastRunAt = table.Column<DateTime>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DatabaseBackupSettings", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_DatabaseBackups_CreatedAt",
                table: "DatabaseBackups",
                column: "CreatedAt");

            // Default settings row (Id 1) — 1 day interval, matching FeeScheduleAutoImportBackgroundService's
            // existing daily cadence as a sensible out-of-the-box default. IntervalUnit 2 == Days (see
            // BackupIntervalUnit: Minutes=0, Hours=1, Days=2, Weeks=3, Months=4).
            migrationBuilder.InsertData(
                table: "DatabaseBackupSettings",
                columns: new[] { "Id", "IntervalValue", "IntervalUnit", "LastRunAt" },
                values: new object[] { 1, 1, 2, null });

            // Grant the new page key directly (rather than relying on the app-startup seed, which only
            // runs against an empty RolePermissions table and is a no-op on an existing DB).
            migrationBuilder.InsertData(
                table: "RolePermissions",
                columns: new[] { "RoleName", "PageKey", "Category" },
                values: new object[,]
                {
                    { "SuperAdmin", "admin/database-backups", "Admin" },
                    { "Admin", "admin/database-backups", "Admin" }
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
                    { "SuperAdmin", "admin/database-backups" },
                    { "Admin", "admin/database-backups" }
                });

            migrationBuilder.DropTable(
                name: "DatabaseBackups");

            migrationBuilder.DropTable(
                name: "DatabaseBackupSettings");
        }
    }
}
