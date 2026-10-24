using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace IntelliMed.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class RemoveReferralAndEmployerFreeText : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ReferringProviderName",
                table: "ClientReferrals");

            migrationBuilder.DropColumn(
                name: "ReferringProviderNumber",
                table: "ClientReferrals");

            migrationBuilder.DropColumn(
                name: "Employer",
                table: "ClientOccupations");

            migrationBuilder.DropColumn(
                name: "EmployerName",
                table: "ClientCompensationClaims");

            migrationBuilder.AlterColumn<int>(
                name: "ReferringContactId",
                table: "ClientReferrals",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0,
                oldClrType: typeof(int),
                oldType: "INTEGER",
                oldNullable: true);

            migrationBuilder.AddColumn<string>(
                name: "LegacyGuid",
                table: "AddressBookContacts",
                type: "TEXT",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_AddressBookContacts_LegacyGuid",
                table: "AddressBookContacts",
                column: "LegacyGuid",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_AddressBookContacts_LegacyGuid",
                table: "AddressBookContacts");

            migrationBuilder.DropColumn(
                name: "LegacyGuid",
                table: "AddressBookContacts");

            migrationBuilder.AlterColumn<int>(
                name: "ReferringContactId",
                table: "ClientReferrals",
                type: "INTEGER",
                nullable: true,
                oldClrType: typeof(int),
                oldType: "INTEGER");

            migrationBuilder.AddColumn<string>(
                name: "ReferringProviderName",
                table: "ClientReferrals",
                type: "TEXT",
                maxLength: 200,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "ReferringProviderNumber",
                table: "ClientReferrals",
                type: "TEXT",
                maxLength: 20,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Employer",
                table: "ClientOccupations",
                type: "TEXT",
                maxLength: 255,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "EmployerName",
                table: "ClientCompensationClaims",
                type: "TEXT",
                maxLength: 200,
                nullable: true);

            // Rebuild the free text from the links so rolling back loses nothing.
            migrationBuilder.Sql(@"
                UPDATE ClientReferrals SET
                    ReferringProviderName = IFNULL((SELECT TRIM(IFNULL(c.Title || ' ', '') || IFNULL(c.FirstName || ' ', '') || c.LastName)
                                                    FROM AddressBookContacts c WHERE c.Id = ClientReferrals.ReferringContactId), ''),
                    ReferringProviderNumber = (SELECT c.ProviderNumber FROM AddressBookContacts c WHERE c.Id = ClientReferrals.ReferringContactId);");
            migrationBuilder.Sql(@"
                UPDATE ClientOccupations SET Employer = (SELECT x.LastName FROM Clients x WHERE x.Id = ClientOccupations.EmployerClientId)
                WHERE EmployerClientId IS NOT NULL;");
            migrationBuilder.Sql(@"
                UPDATE ClientCompensationClaims SET EmployerName = (SELECT x.LastName FROM Clients x WHERE x.Id = ClientCompensationClaims.EmployerClientId)
                WHERE EmployerClientId IS NOT NULL;");
        }
    }
}
