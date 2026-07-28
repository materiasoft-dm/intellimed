using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace IntelliMed.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddInvoiceItemFeeScheduleId : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "FeeScheduleId",
                table: "InvoiceItems",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_InvoiceItems_FeeScheduleId",
                table: "InvoiceItems",
                column: "FeeScheduleId");

            migrationBuilder.AddForeignKey(
                name: "FK_InvoiceItems_FeeSchedules_FeeScheduleId",
                table: "InvoiceItems",
                column: "FeeScheduleId",
                principalTable: "FeeSchedules",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_InvoiceItems_FeeSchedules_FeeScheduleId",
                table: "InvoiceItems");

            migrationBuilder.DropIndex(
                name: "IX_InvoiceItems_FeeScheduleId",
                table: "InvoiceItems");

            migrationBuilder.DropColumn(
                name: "FeeScheduleId",
                table: "InvoiceItems");
        }
    }
}
