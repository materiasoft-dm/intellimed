using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace IntelliMed.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddEmailInfrastructure : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "SmtpEnabled",
                table: "ClinicSettings",
                type: "INTEGER",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "SmtpFromEmail",
                table: "ClinicSettings",
                type: "TEXT",
                maxLength: 255,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "SmtpFromName",
                table: "ClinicSettings",
                type: "TEXT",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "SmtpHost",
                table: "ClinicSettings",
                type: "TEXT",
                maxLength: 255,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "SmtpPassword",
                table: "ClinicSettings",
                type: "TEXT",
                maxLength: 255,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "SmtpPort",
                table: "ClinicSettings",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<bool>(
                name: "SmtpUseSsl",
                table: "ClinicSettings",
                type: "INTEGER",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "SmtpUsername",
                table: "ClinicSettings",
                type: "TEXT",
                maxLength: 255,
                nullable: true);

            migrationBuilder.CreateTable(
                name: "EmailTemplates",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    ClinicId = table.Column<int>(type: "INTEGER", nullable: false),
                    Name = table.Column<string>(type: "TEXT", maxLength: 150, nullable: false),
                    Subject = table.Column<string>(type: "TEXT", maxLength: 255, nullable: false),
                    BodyHtml = table.Column<string>(type: "TEXT", nullable: false),
                    EventKey = table.Column<string>(type: "TEXT", maxLength: 50, nullable: true),
                    IsArchived = table.Column<bool>(type: "INTEGER", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EmailTemplates", x => x.Id);
                });

            migrationBuilder.UpdateData(
                table: "ClinicSettings",
                keyColumn: "Id",
                keyValue: 1,
                columns: new[] { "SmtpEnabled", "SmtpFromEmail", "SmtpFromName", "SmtpHost", "SmtpPassword", "SmtpPort", "SmtpUseSsl", "SmtpUsername" },
                values: new object[] { false, null, null, null, null, 587, true, null });

            migrationBuilder.CreateIndex(
                name: "IX_EmailTemplates_ClinicId_EventKey",
                table: "EmailTemplates",
                columns: new[] { "ClinicId", "EventKey" });

            // Seed default templates for the two built-in system events so Send Invite and
            // Forgot Password work out of the box without an admin having to author one first.
            migrationBuilder.InsertData(
                table: "EmailTemplates",
                columns: new[] { "Id", "ClinicId", "Name", "Subject", "BodyHtml", "EventKey", "IsArchived", "CreatedAt", "UpdatedAt" },
                values: new object[]
                {
                    1, 1, "Default Invite Email", "You've been invited to IntelliMed",
                    "<p>Hi {{FirstName}},</p>" +
                    "<p>You've been invited to IntelliMed as <strong>{{RoleNames}}</strong> for <strong>{{ClinicNames}}</strong>.</p>" +
                    "<p>Your temporary password is: <strong>{{GeneratedPassword}}</strong></p>" +
                    "<p><a href=\"{{InviteLink}}\">Click here to set your password and finish signing in</a></p>" +
                    "<p>If the button doesn't work, copy and paste this link into your browser:<br />{{InviteLink}}</p>" +
                    "<p>Thanks,<br />IntelliMed</p>",
                    "InviteEmail", false, new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc), new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc)
                });

            migrationBuilder.InsertData(
                table: "EmailTemplates",
                columns: new[] { "Id", "ClinicId", "Name", "Subject", "BodyHtml", "EventKey", "IsArchived", "CreatedAt", "UpdatedAt" },
                values: new object[]
                {
                    2, 1, "Default Forgot Password", "Reset your IntelliMed password",
                    "<p>Hi {{FirstName}},</p>" +
                    "<p>We received a request to reset the password for your IntelliMed account ({{Email}}).</p>" +
                    "<p><a href=\"{{ResetLink}}\">Click here to reset your password</a></p>" +
                    "<p>If the button doesn't work, copy and paste this link into your browser:<br />{{ResetLink}}</p>" +
                    "<p>If you didn't request this, you can safely ignore this email.</p>" +
                    "<p>Thanks,<br />IntelliMed</p>",
                    "ForgotPassword", false, new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc), new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc)
                });

            // Grants the new "admin/email-templates" page to SuperAdmin/Admin on existing databases
            // (Program.cs's SeedRolePermissionsAsync only runs against an empty RolePermissions table).
            migrationBuilder.InsertData(
                table: "RolePermissions",
                columns: new[] { "RoleName", "PageKey", "Category" },
                values: new object[,]
                {
                    { "SuperAdmin", "admin/email-templates", "Admin" },
                    { "Admin", "admin/email-templates", "Admin" }
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
                    { "SuperAdmin", "admin/email-templates" },
                    { "Admin", "admin/email-templates" }
                });

            migrationBuilder.DeleteData(
                table: "EmailTemplates",
                keyColumn: "Id",
                keyValue: 1);

            migrationBuilder.DeleteData(
                table: "EmailTemplates",
                keyColumn: "Id",
                keyValue: 2);

            migrationBuilder.DropTable(
                name: "EmailTemplates");

            migrationBuilder.DropColumn(
                name: "SmtpEnabled",
                table: "ClinicSettings");

            migrationBuilder.DropColumn(
                name: "SmtpFromEmail",
                table: "ClinicSettings");

            migrationBuilder.DropColumn(
                name: "SmtpFromName",
                table: "ClinicSettings");

            migrationBuilder.DropColumn(
                name: "SmtpHost",
                table: "ClinicSettings");

            migrationBuilder.DropColumn(
                name: "SmtpPassword",
                table: "ClinicSettings");

            migrationBuilder.DropColumn(
                name: "SmtpPort",
                table: "ClinicSettings");

            migrationBuilder.DropColumn(
                name: "SmtpUseSsl",
                table: "ClinicSettings");

            migrationBuilder.DropColumn(
                name: "SmtpUsername",
                table: "ClinicSettings");
        }
    }
}
