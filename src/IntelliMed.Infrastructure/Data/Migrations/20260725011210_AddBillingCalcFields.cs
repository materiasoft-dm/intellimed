using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace IntelliMed.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddBillingCalcFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "ServiceType",
                table: "Practitioners",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "PlaceOfService",
                table: "Invoices",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<decimal>(
                name: "Discount",
                table: "InvoiceItems",
                type: "TEXT",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<bool>(
                name: "FeeIncludeGst",
                table: "InvoiceItems",
                type: "INTEGER",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<decimal>(
                name: "GstAmount",
                table: "InvoiceItems",
                type: "TEXT",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "PercentGst",
                table: "InvoiceItems",
                type: "TEXT",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "RebatePerUnit",
                table: "InvoiceItems",
                type: "TEXT",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.CreateTable(
                name: "AccountTypeFeeScheduleMappings",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    ClinicId = table.Column<int>(type: "INTEGER", nullable: false),
                    AccountType = table.Column<int>(type: "INTEGER", nullable: false),
                    FeeScheduleId = table.Column<int>(type: "INTEGER", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AccountTypeFeeScheduleMappings", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AccountTypeFeeScheduleMappings_FeeSchedules_FeeScheduleId",
                        column: x => x.FeeScheduleId,
                        principalTable: "FeeSchedules",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_AccountTypeFeeScheduleMappings_ClinicId_AccountType",
                table: "AccountTypeFeeScheduleMappings",
                columns: new[] { "ClinicId", "AccountType" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_AccountTypeFeeScheduleMappings_FeeScheduleId",
                table: "AccountTypeFeeScheduleMappings",
                column: "FeeScheduleId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AccountTypeFeeScheduleMappings");

            migrationBuilder.DropColumn(
                name: "ServiceType",
                table: "Practitioners");

            migrationBuilder.DropColumn(
                name: "PlaceOfService",
                table: "Invoices");

            migrationBuilder.DropColumn(
                name: "Discount",
                table: "InvoiceItems");

            migrationBuilder.DropColumn(
                name: "FeeIncludeGst",
                table: "InvoiceItems");

            migrationBuilder.DropColumn(
                name: "GstAmount",
                table: "InvoiceItems");

            migrationBuilder.DropColumn(
                name: "PercentGst",
                table: "InvoiceItems");

            migrationBuilder.DropColumn(
                name: "RebatePerUnit",
                table: "InvoiceItems");
        }
    }
}
