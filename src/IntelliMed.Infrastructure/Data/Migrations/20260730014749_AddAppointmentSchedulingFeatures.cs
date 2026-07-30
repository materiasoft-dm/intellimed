using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace IntelliMed.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddAppointmentSchedulingFeatures : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "AppointmentTypeSettingId",
                table: "Appointments",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "ArrivedAt",
                table: "Appointments",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "CompletedAt",
                table: "Appointments",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "RecurrenceSeriesId",
                table: "Appointments",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "SeenAt",
                table: "Appointments",
                type: "TEXT",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "AppointmentTypeSettings",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    ClinicId = table.Column<int>(type: "INTEGER", nullable: false),
                    Name = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    DefaultDurationMinutes = table.Column<int>(type: "INTEGER", nullable: false),
                    ColorHex = table.Column<string>(type: "TEXT", maxLength: 20, nullable: false),
                    IsActive = table.Column<bool>(type: "INTEGER", nullable: false),
                    SortOrder = table.Column<int>(type: "INTEGER", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AppointmentTypeSettings", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ProviderSchedules",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    ApplicationUserId = table.Column<string>(type: "TEXT", nullable: false),
                    DayOfWeek = table.Column<int>(type: "INTEGER", nullable: false),
                    StartTime = table.Column<TimeSpan>(type: "TEXT", nullable: false),
                    EndTime = table.Column<TimeSpan>(type: "TEXT", nullable: false),
                    IsActive = table.Column<bool>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ProviderSchedules", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ProviderSchedules_AspNetUsers_ApplicationUserId",
                        column: x => x.ApplicationUserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Appointments_AppointmentTypeSettingId",
                table: "Appointments",
                column: "AppointmentTypeSettingId");

            migrationBuilder.CreateIndex(
                name: "IX_Appointments_RecurrenceSeriesId",
                table: "Appointments",
                column: "RecurrenceSeriesId");

            migrationBuilder.CreateIndex(
                name: "IX_AppointmentTypeSettings_ClinicId",
                table: "AppointmentTypeSettings",
                column: "ClinicId");

            migrationBuilder.CreateIndex(
                name: "IX_ProviderSchedules_ApplicationUserId_DayOfWeek",
                table: "ProviderSchedules",
                columns: new[] { "ApplicationUserId", "DayOfWeek" },
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_Appointments_AppointmentTypeSettings_AppointmentTypeSettingId",
                table: "Appointments",
                column: "AppointmentTypeSettingId",
                principalTable: "AppointmentTypeSettings",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);

            // Grant the two new page keys directly (rather than relying on the app-startup seed,
            // which only runs against an empty RolePermissions table and is a no-op on an existing DB).
            migrationBuilder.InsertData(
                table: "RolePermissions",
                columns: new[] { "RoleName", "PageKey", "Category" },
                values: new object[,]
                {
                    { "SuperAdmin", "appointments/waiting-room", "Clinical" },
                    { "Admin", "appointments/waiting-room", "Clinical" },
                    { "Doctor", "appointments/waiting-room", "Clinical" },
                    { "Nurse", "appointments/waiting-room", "Clinical" },
                    { "Receptionist", "appointments/waiting-room", "Clinical" },
                    { "SuperAdmin", "admin/appointment-types", "Admin" },
                    { "Admin", "admin/appointment-types", "Admin" }
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
                    { "SuperAdmin", "appointments/waiting-room" },
                    { "Admin", "appointments/waiting-room" },
                    { "Doctor", "appointments/waiting-room" },
                    { "Nurse", "appointments/waiting-room" },
                    { "Receptionist", "appointments/waiting-room" },
                    { "SuperAdmin", "admin/appointment-types" },
                    { "Admin", "admin/appointment-types" }
                });

            migrationBuilder.DropForeignKey(
                name: "FK_Appointments_AppointmentTypeSettings_AppointmentTypeSettingId",
                table: "Appointments");

            migrationBuilder.DropTable(
                name: "AppointmentTypeSettings");

            migrationBuilder.DropTable(
                name: "ProviderSchedules");

            migrationBuilder.DropIndex(
                name: "IX_Appointments_AppointmentTypeSettingId",
                table: "Appointments");

            migrationBuilder.DropIndex(
                name: "IX_Appointments_RecurrenceSeriesId",
                table: "Appointments");

            migrationBuilder.DropColumn(
                name: "AppointmentTypeSettingId",
                table: "Appointments");

            migrationBuilder.DropColumn(
                name: "ArrivedAt",
                table: "Appointments");

            migrationBuilder.DropColumn(
                name: "CompletedAt",
                table: "Appointments");

            migrationBuilder.DropColumn(
                name: "RecurrenceSeriesId",
                table: "Appointments");

            migrationBuilder.DropColumn(
                name: "SeenAt",
                table: "Appointments");
        }
    }
}
