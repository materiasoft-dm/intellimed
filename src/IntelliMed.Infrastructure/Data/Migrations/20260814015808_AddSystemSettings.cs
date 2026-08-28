using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace IntelliMed.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddSystemSettings : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "SystemSettings",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    PasswordMinLength = table.Column<int>(type: "INTEGER", nullable: false),
                    PasswordRequireDigit = table.Column<bool>(type: "INTEGER", nullable: false),
                    PasswordRequireLowercase = table.Column<bool>(type: "INTEGER", nullable: false),
                    PasswordRequireUppercase = table.Column<bool>(type: "INTEGER", nullable: false),
                    PasswordRequireNonAlphanumeric = table.Column<bool>(type: "INTEGER", nullable: false),
                    LockoutMaxFailedAttempts = table.Column<int>(type: "INTEGER", nullable: false),
                    LockoutDurationMinutes = table.Column<int>(type: "INTEGER", nullable: false),
                    SessionExpirationHours = table.Column<int>(type: "INTEGER", nullable: false),
                    DefaultAppointmentSlotMinutes = table.Column<int>(type: "INTEGER", nullable: false),
                    NewUserShowCommandPalette = table.Column<bool>(type: "INTEGER", nullable: false),
                    BackupRetentionDays = table.Column<int>(type: "INTEGER", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SystemSettings", x => x.Id);
                });

            migrationBuilder.InsertData(
                table: "SystemSettings",
                columns: new[] { "Id", "BackupRetentionDays", "DefaultAppointmentSlotMinutes", "LockoutDurationMinutes", "LockoutMaxFailedAttempts", "NewUserShowCommandPalette", "PasswordMinLength", "PasswordRequireDigit", "PasswordRequireLowercase", "PasswordRequireNonAlphanumeric", "PasswordRequireUppercase", "SessionExpirationHours", "UpdatedAt" },
                values: new object[] { 1, 60, 15, 15, 5, true, 8, true, true, true, true, 8, null });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "SystemSettings");
        }
    }
}
