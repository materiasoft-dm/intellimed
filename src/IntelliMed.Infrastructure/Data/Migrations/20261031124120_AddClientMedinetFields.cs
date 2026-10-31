using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace IntelliMed.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddClientMedinetFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "AtsiAccreditation",
                table: "Clients",
                type: "TEXT",
                maxLength: 50,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ConcessionCardType",
                table: "Clients",
                type: "TEXT",
                maxLength: 20,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CountryOfBirth",
                table: "Clients",
                type: "TEXT",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CulturalBackground",
                table: "Clients",
                type: "TEXT",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "DateOfDeath",
                table: "Clients",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "DisableEPrescribing",
                table: "Clients",
                type: "INTEGER",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "DvaCardType",
                table: "Clients",
                type: "TEXT",
                maxLength: 20,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "EmergencyContactRelationship",
                table: "Clients",
                type: "TEXT",
                maxLength: 50,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ExternalId",
                table: "Clients",
                type: "TEXT",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "HealthCareCardDate",
                table: "Clients",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "HealthFundCoverLevel",
                table: "Clients",
                type: "TEXT",
                maxLength: 50,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "HowDidYouHear",
                table: "Clients",
                type: "TEXT",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "MinorFileNumber",
                table: "Clients",
                type: "TEXT",
                maxLength: 20,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "NextOfKinRelationship",
                table: "Clients",
                type: "TEXT",
                maxLength: 50,
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "OneInvoicePerPatient",
                table: "Clients",
                type: "INTEGER",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "PatientPortalId",
                table: "Clients",
                type: "TEXT",
                maxLength: 50,
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "PaysByEft",
                table: "Clients",
                type: "INTEGER",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "PbsEntitlementCardType",
                table: "Clients",
                type: "TEXT",
                maxLength: 20,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "PbsEntitlementExpiryDate",
                table: "Clients",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PbsEntitlementNumber",
                table: "Clients",
                type: "TEXT",
                maxLength: 20,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PreferredLanguage",
                table: "Clients",
                type: "TEXT",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PreferredPharmacyCode1",
                table: "Clients",
                type: "TEXT",
                maxLength: 20,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PreferredPharmacyCode2",
                table: "Clients",
                type: "TEXT",
                maxLength: 20,
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "ResearchConsent",
                table: "Clients",
                type: "INTEGER",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "ResidentialAddressType",
                table: "Clients",
                type: "TEXT",
                maxLength: 50,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "SafetyNetCardType",
                table: "Clients",
                type: "TEXT",
                maxLength: 20,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "SafetyNetExpiryDate",
                table: "Clients",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Salutation",
                table: "Clients",
                type: "TEXT",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "SendToPostalAddress",
                table: "Clients",
                type: "INTEGER",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "ShowEPrescriptionMessage",
                table: "Clients",
                type: "INTEGER",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<DateTime>(
                name: "StatementHoldUntil",
                table: "Clients",
                type: "TEXT",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "AtsiAccreditation",
                table: "Clients");

            migrationBuilder.DropColumn(
                name: "ConcessionCardType",
                table: "Clients");

            migrationBuilder.DropColumn(
                name: "CountryOfBirth",
                table: "Clients");

            migrationBuilder.DropColumn(
                name: "CulturalBackground",
                table: "Clients");

            migrationBuilder.DropColumn(
                name: "DateOfDeath",
                table: "Clients");

            migrationBuilder.DropColumn(
                name: "DisableEPrescribing",
                table: "Clients");

            migrationBuilder.DropColumn(
                name: "DvaCardType",
                table: "Clients");

            migrationBuilder.DropColumn(
                name: "EmergencyContactRelationship",
                table: "Clients");

            migrationBuilder.DropColumn(
                name: "ExternalId",
                table: "Clients");

            migrationBuilder.DropColumn(
                name: "HealthCareCardDate",
                table: "Clients");

            migrationBuilder.DropColumn(
                name: "HealthFundCoverLevel",
                table: "Clients");

            migrationBuilder.DropColumn(
                name: "HowDidYouHear",
                table: "Clients");

            migrationBuilder.DropColumn(
                name: "MinorFileNumber",
                table: "Clients");

            migrationBuilder.DropColumn(
                name: "NextOfKinRelationship",
                table: "Clients");

            migrationBuilder.DropColumn(
                name: "OneInvoicePerPatient",
                table: "Clients");

            migrationBuilder.DropColumn(
                name: "PatientPortalId",
                table: "Clients");

            migrationBuilder.DropColumn(
                name: "PaysByEft",
                table: "Clients");

            migrationBuilder.DropColumn(
                name: "PbsEntitlementCardType",
                table: "Clients");

            migrationBuilder.DropColumn(
                name: "PbsEntitlementExpiryDate",
                table: "Clients");

            migrationBuilder.DropColumn(
                name: "PbsEntitlementNumber",
                table: "Clients");

            migrationBuilder.DropColumn(
                name: "PreferredLanguage",
                table: "Clients");

            migrationBuilder.DropColumn(
                name: "PreferredPharmacyCode1",
                table: "Clients");

            migrationBuilder.DropColumn(
                name: "PreferredPharmacyCode2",
                table: "Clients");

            migrationBuilder.DropColumn(
                name: "ResearchConsent",
                table: "Clients");

            migrationBuilder.DropColumn(
                name: "ResidentialAddressType",
                table: "Clients");

            migrationBuilder.DropColumn(
                name: "SafetyNetCardType",
                table: "Clients");

            migrationBuilder.DropColumn(
                name: "SafetyNetExpiryDate",
                table: "Clients");

            migrationBuilder.DropColumn(
                name: "Salutation",
                table: "Clients");

            migrationBuilder.DropColumn(
                name: "SendToPostalAddress",
                table: "Clients");

            migrationBuilder.DropColumn(
                name: "ShowEPrescriptionMessage",
                table: "Clients");

            migrationBuilder.DropColumn(
                name: "StatementHoldUntil",
                table: "Clients");
        }
    }
}
