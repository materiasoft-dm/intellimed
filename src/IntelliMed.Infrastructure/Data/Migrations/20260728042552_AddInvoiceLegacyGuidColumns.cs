using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace IntelliMed.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddInvoiceLegacyGuidColumns : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "LegacyGuid",
                table: "Payments",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "LegacyGuid",
                table: "Invoices",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "LegacyGuid",
                table: "InvoiceItems",
                type: "TEXT",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Payments_LegacyGuid",
                table: "Payments",
                column: "LegacyGuid",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Invoices_LegacyGuid",
                table: "Invoices",
                column: "LegacyGuid",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_InvoiceItems_LegacyGuid",
                table: "InvoiceItems",
                column: "LegacyGuid",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Payments_LegacyGuid",
                table: "Payments");

            migrationBuilder.DropIndex(
                name: "IX_Invoices_LegacyGuid",
                table: "Invoices");

            migrationBuilder.DropIndex(
                name: "IX_InvoiceItems_LegacyGuid",
                table: "InvoiceItems");

            migrationBuilder.DropColumn(
                name: "LegacyGuid",
                table: "Payments");

            migrationBuilder.DropColumn(
                name: "LegacyGuid",
                table: "Invoices");

            migrationBuilder.DropColumn(
                name: "LegacyGuid",
                table: "InvoiceItems");
        }
    }
}
