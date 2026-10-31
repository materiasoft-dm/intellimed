using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace IntelliMed.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddAddressBookContactMedinetFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Abn",
                table: "AddressBookContacts",
                type: "TEXT",
                maxLength: 20,
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "AcceptsSecureMessaging",
                table: "AddressBookContacts",
                type: "INTEGER",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "Acn",
                table: "AddressBookContacts",
                type: "TEXT",
                maxLength: 20,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "AddressLine2",
                table: "AddressBookContacts",
                type: "TEXT",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Attention",
                table: "AddressBookContacts",
                type: "TEXT",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ExternalId",
                table: "AddressBookContacts",
                type: "TEXT",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "Gender",
                table: "AddressBookContacts",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Hpii",
                table: "AddressBookContacts",
                type: "TEXT",
                maxLength: 20,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "MessagingId",
                table: "AddressBookContacts",
                type: "TEXT",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "MessagingNetwork",
                table: "AddressBookContacts",
                type: "TEXT",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PreferredName",
                table: "AddressBookContacts",
                type: "TEXT",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ProviderTypeCode",
                table: "AddressBookContacts",
                type: "TEXT",
                maxLength: 20,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Abn",
                table: "AddressBookContacts");

            migrationBuilder.DropColumn(
                name: "AcceptsSecureMessaging",
                table: "AddressBookContacts");

            migrationBuilder.DropColumn(
                name: "Acn",
                table: "AddressBookContacts");

            migrationBuilder.DropColumn(
                name: "AddressLine2",
                table: "AddressBookContacts");

            migrationBuilder.DropColumn(
                name: "Attention",
                table: "AddressBookContacts");

            migrationBuilder.DropColumn(
                name: "ExternalId",
                table: "AddressBookContacts");

            migrationBuilder.DropColumn(
                name: "Gender",
                table: "AddressBookContacts");

            migrationBuilder.DropColumn(
                name: "Hpii",
                table: "AddressBookContacts");

            migrationBuilder.DropColumn(
                name: "MessagingId",
                table: "AddressBookContacts");

            migrationBuilder.DropColumn(
                name: "MessagingNetwork",
                table: "AddressBookContacts");

            migrationBuilder.DropColumn(
                name: "PreferredName",
                table: "AddressBookContacts");

            migrationBuilder.DropColumn(
                name: "ProviderTypeCode",
                table: "AddressBookContacts");
        }
    }
}
