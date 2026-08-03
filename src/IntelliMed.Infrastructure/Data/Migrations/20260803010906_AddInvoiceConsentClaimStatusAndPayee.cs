using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace IntelliMed.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddInvoiceConsentClaimStatusAndPayee : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "BenefitAssignmentRequested",
                table: "Invoices",
                type: "INTEGER",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<int>(
                name: "ClaimStatus",
                table: "Invoices",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<bool>(
                name: "ClaimSubmissionAuthorised",
                table: "Invoices",
                type: "INTEGER",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "CompensationClaim",
                table: "Invoices",
                type: "INTEGER",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "FinancialInterestDisclosed",
                table: "Invoices",
                type: "INTEGER",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<int>(
                name: "PayeePractitionerId",
                table: "Invoices",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "SubmissionAuthorityReceived",
                table: "Invoices",
                type: "INTEGER",
                nullable: false,
                defaultValue: false);

            migrationBuilder.CreateIndex(
                name: "IX_Invoices_PayeePractitionerId",
                table: "Invoices",
                column: "PayeePractitionerId");

            migrationBuilder.AddForeignKey(
                name: "FK_Invoices_Practitioners_PayeePractitionerId",
                table: "Invoices",
                column: "PayeePractitionerId",
                principalTable: "Practitioners",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Invoices_Practitioners_PayeePractitionerId",
                table: "Invoices");

            migrationBuilder.DropIndex(
                name: "IX_Invoices_PayeePractitionerId",
                table: "Invoices");

            migrationBuilder.DropColumn(
                name: "BenefitAssignmentRequested",
                table: "Invoices");

            migrationBuilder.DropColumn(
                name: "ClaimStatus",
                table: "Invoices");

            migrationBuilder.DropColumn(
                name: "ClaimSubmissionAuthorised",
                table: "Invoices");

            migrationBuilder.DropColumn(
                name: "CompensationClaim",
                table: "Invoices");

            migrationBuilder.DropColumn(
                name: "FinancialInterestDisclosed",
                table: "Invoices");

            migrationBuilder.DropColumn(
                name: "PayeePractitionerId",
                table: "Invoices");

            migrationBuilder.DropColumn(
                name: "SubmissionAuthorityReceived",
                table: "Invoices");
        }
    }
}
