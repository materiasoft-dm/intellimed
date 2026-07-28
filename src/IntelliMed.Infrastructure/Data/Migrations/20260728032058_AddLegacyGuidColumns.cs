using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace IntelliMed.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddLegacyGuidColumns : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "LegacyGuid",
                table: "Clients",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "LegacyGuid",
                table: "ClientReferrals",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "LegacyGuid",
                table: "ClientOccupations",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "LegacyGuid",
                table: "ClientFamilyRelationships",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "LegacyGuid",
                table: "ClientCompensationClaims",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "LegacyGuid",
                table: "ClientAddresses",
                type: "TEXT",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Clients_LegacyGuid",
                table: "Clients",
                column: "LegacyGuid",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ClientReferrals_LegacyGuid",
                table: "ClientReferrals",
                column: "LegacyGuid",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ClientOccupations_LegacyGuid",
                table: "ClientOccupations",
                column: "LegacyGuid",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ClientFamilyRelationships_LegacyGuid",
                table: "ClientFamilyRelationships",
                column: "LegacyGuid",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ClientCompensationClaims_LegacyGuid",
                table: "ClientCompensationClaims",
                column: "LegacyGuid",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ClientAddresses_LegacyGuid",
                table: "ClientAddresses",
                column: "LegacyGuid",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Clients_LegacyGuid",
                table: "Clients");

            migrationBuilder.DropIndex(
                name: "IX_ClientReferrals_LegacyGuid",
                table: "ClientReferrals");

            migrationBuilder.DropIndex(
                name: "IX_ClientOccupations_LegacyGuid",
                table: "ClientOccupations");

            migrationBuilder.DropIndex(
                name: "IX_ClientFamilyRelationships_LegacyGuid",
                table: "ClientFamilyRelationships");

            migrationBuilder.DropIndex(
                name: "IX_ClientCompensationClaims_LegacyGuid",
                table: "ClientCompensationClaims");

            migrationBuilder.DropIndex(
                name: "IX_ClientAddresses_LegacyGuid",
                table: "ClientAddresses");

            migrationBuilder.DropColumn(
                name: "LegacyGuid",
                table: "Clients");

            migrationBuilder.DropColumn(
                name: "LegacyGuid",
                table: "ClientReferrals");

            migrationBuilder.DropColumn(
                name: "LegacyGuid",
                table: "ClientOccupations");

            migrationBuilder.DropColumn(
                name: "LegacyGuid",
                table: "ClientFamilyRelationships");

            migrationBuilder.DropColumn(
                name: "LegacyGuid",
                table: "ClientCompensationClaims");

            migrationBuilder.DropColumn(
                name: "LegacyGuid",
                table: "ClientAddresses");
        }
    }
}
