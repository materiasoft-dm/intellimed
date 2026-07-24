using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace IntelliMed.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddBillingItemsAndInvoiceEnhancements : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "AccountType",
                table: "Invoices",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "PractitionerId",
                table: "Invoices",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "BillingItemId",
                table: "InvoiceItems",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "ServiceDate",
                table: "InvoiceItems",
                type: "TEXT",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "BillingItems",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    ItemNumber = table.Column<string>(type: "TEXT", maxLength: 20, nullable: false),
                    Category = table.Column<string>(type: "TEXT", maxLength: 10, nullable: true),
                    Group = table.Column<string>(type: "TEXT", maxLength: 10, nullable: true),
                    Description = table.Column<string>(type: "TEXT", nullable: false),
                    ScheduleFee = table.Column<decimal>(type: "TEXT", nullable: false),
                    Benefit100 = table.Column<decimal>(type: "TEXT", nullable: true),
                    ItemStartDate = table.Column<DateTime>(type: "TEXT", nullable: true),
                    ItemEndDate = table.Column<DateTime>(type: "TEXT", nullable: true),
                    IsActive = table.Column<bool>(type: "INTEGER", nullable: false),
                    LastSyncedAt = table.Column<DateTime>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BillingItems", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Invoices_PractitionerId",
                table: "Invoices",
                column: "PractitionerId");

            migrationBuilder.CreateIndex(
                name: "IX_InvoiceItems_BillingItemId",
                table: "InvoiceItems",
                column: "BillingItemId");

            migrationBuilder.CreateIndex(
                name: "IX_BillingItems_ItemNumber",
                table: "BillingItems",
                column: "ItemNumber",
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_InvoiceItems_BillingItems_BillingItemId",
                table: "InvoiceItems",
                column: "BillingItemId",
                principalTable: "BillingItems",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Invoices_Practitioners_PractitionerId",
                table: "Invoices",
                column: "PractitionerId",
                principalTable: "Practitioners",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_InvoiceItems_BillingItems_BillingItemId",
                table: "InvoiceItems");

            migrationBuilder.DropForeignKey(
                name: "FK_Invoices_Practitioners_PractitionerId",
                table: "Invoices");

            migrationBuilder.DropTable(
                name: "BillingItems");

            migrationBuilder.DropIndex(
                name: "IX_Invoices_PractitionerId",
                table: "Invoices");

            migrationBuilder.DropIndex(
                name: "IX_InvoiceItems_BillingItemId",
                table: "InvoiceItems");

            migrationBuilder.DropColumn(
                name: "AccountType",
                table: "Invoices");

            migrationBuilder.DropColumn(
                name: "PractitionerId",
                table: "Invoices");

            migrationBuilder.DropColumn(
                name: "BillingItemId",
                table: "InvoiceItems");

            migrationBuilder.DropColumn(
                name: "ServiceDate",
                table: "InvoiceItems");
        }
    }
}
