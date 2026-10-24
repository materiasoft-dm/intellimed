using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace IntelliMed.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddAddressBookAndClinicType : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "Type",
                table: "Clinics",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AlterColumn<DateTime>(
                name: "DateOfBirth",
                table: "Clients",
                type: "TEXT",
                nullable: true,
                oldClrType: typeof(DateTime),
                oldType: "TEXT");

            migrationBuilder.AddColumn<int>(
                name: "ReferringContactId",
                table: "ClientReferrals",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "EmployerClientId",
                table: "ClientOccupations",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "EmployerClientId",
                table: "ClientCompensationClaims",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "AddressBookContacts",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Title = table.Column<string>(type: "TEXT", maxLength: 20, nullable: true),
                    FirstName = table.Column<string>(type: "TEXT", maxLength: 100, nullable: true),
                    LastName = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
                    Profession = table.Column<string>(type: "TEXT", maxLength: 100, nullable: true),
                    Speciality = table.Column<string>(type: "TEXT", maxLength: 100, nullable: true),
                    ProviderNumber = table.Column<string>(type: "TEXT", maxLength: 20, nullable: true),
                    Phone = table.Column<string>(type: "TEXT", maxLength: 50, nullable: true),
                    Mobile = table.Column<string>(type: "TEXT", maxLength: 50, nullable: true),
                    Fax = table.Column<string>(type: "TEXT", maxLength: 50, nullable: true),
                    Email = table.Column<string>(type: "TEXT", maxLength: 200, nullable: true),
                    Address = table.Column<string>(type: "TEXT", maxLength: 200, nullable: true),
                    Suburb = table.Column<string>(type: "TEXT", maxLength: 100, nullable: true),
                    Postcode = table.Column<string>(type: "TEXT", maxLength: 10, nullable: true),
                    State = table.Column<string>(type: "TEXT", maxLength: 10, nullable: true),
                    ClinicId = table.Column<int>(type: "INTEGER", nullable: true),
                    Notes = table.Column<string>(type: "TEXT", nullable: true),
                    IsArchived = table.Column<bool>(type: "INTEGER", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AddressBookContacts", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AddressBookContacts_Clinics_ClinicId",
                        column: x => x.ClinicId,
                        principalTable: "Clinics",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.UpdateData(
                table: "Clinics",
                keyColumn: "Id",
                keyValue: 1,
                column: "Type",
                value: 0);

            migrationBuilder.CreateIndex(
                name: "IX_ClientReferrals_ReferringContactId",
                table: "ClientReferrals",
                column: "ReferringContactId");

            migrationBuilder.CreateIndex(
                name: "IX_ClientOccupations_EmployerClientId",
                table: "ClientOccupations",
                column: "EmployerClientId");

            migrationBuilder.CreateIndex(
                name: "IX_ClientCompensationClaims_EmployerClientId",
                table: "ClientCompensationClaims",
                column: "EmployerClientId");

            migrationBuilder.CreateIndex(
                name: "IX_AddressBookContacts_ClinicId",
                table: "AddressBookContacts",
                column: "ClinicId");

            migrationBuilder.CreateIndex(
                name: "IX_AddressBookContacts_LastName",
                table: "AddressBookContacts",
                column: "LastName");

            migrationBuilder.CreateIndex(
                name: "IX_AddressBookContacts_ProviderNumber",
                table: "AddressBookContacts",
                column: "ProviderNumber");

            migrationBuilder.AddForeignKey(
                name: "FK_ClientCompensationClaims_Clients_EmployerClientId",
                table: "ClientCompensationClaims",
                column: "EmployerClientId",
                principalTable: "Clients",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_ClientOccupations_Clients_EmployerClientId",
                table: "ClientOccupations",
                column: "EmployerClientId",
                principalTable: "Clients",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_ClientReferrals_AddressBookContacts_ReferringContactId",
                table: "ClientReferrals",
                column: "ReferringContactId",
                principalTable: "AddressBookContacts",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_ClientCompensationClaims_Clients_EmployerClientId",
                table: "ClientCompensationClaims");

            migrationBuilder.DropForeignKey(
                name: "FK_ClientOccupations_Clients_EmployerClientId",
                table: "ClientOccupations");

            migrationBuilder.DropForeignKey(
                name: "FK_ClientReferrals_AddressBookContacts_ReferringContactId",
                table: "ClientReferrals");

            migrationBuilder.DropTable(
                name: "AddressBookContacts");

            migrationBuilder.DropIndex(
                name: "IX_ClientReferrals_ReferringContactId",
                table: "ClientReferrals");

            migrationBuilder.DropIndex(
                name: "IX_ClientOccupations_EmployerClientId",
                table: "ClientOccupations");

            migrationBuilder.DropIndex(
                name: "IX_ClientCompensationClaims_EmployerClientId",
                table: "ClientCompensationClaims");

            migrationBuilder.DropColumn(
                name: "Type",
                table: "Clinics");

            migrationBuilder.DropColumn(
                name: "ReferringContactId",
                table: "ClientReferrals");

            migrationBuilder.DropColumn(
                name: "EmployerClientId",
                table: "ClientOccupations");

            migrationBuilder.DropColumn(
                name: "EmployerClientId",
                table: "ClientCompensationClaims");


            // Rolling back needs a non-null date again. Organisation clients created from employer names
            // are left in place: they are valid organisations either way.
            migrationBuilder.Sql("UPDATE Clients SET DateOfBirth = '1900-01-01 00:00:00' WHERE DateOfBirth IS NULL;");

            migrationBuilder.AlterColumn<DateTime>(
                name: "DateOfBirth",
                table: "Clients",
                type: "TEXT",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified),
                oldClrType: typeof(DateTime),
                oldType: "TEXT",
                oldNullable: true);
        }
    }
}
