using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace IntelliMed.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class PatientExpansion : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "AcceptEmail",
                table: "Patients",
                type: "INTEGER",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "AcceptOnlineAppointments",
                table: "Patients",
                type: "INTEGER",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "AcceptSms",
                table: "Patients",
                type: "INTEGER",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "AcceptSmsMarketing",
                table: "Patients",
                type: "INTEGER",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "AccountBsb",
                table: "Patients",
                type: "TEXT",
                maxLength: 10,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "AccountName",
                table: "Patients",
                type: "TEXT",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "AccountNumber",
                table: "Patients",
                type: "TEXT",
                maxLength: 30,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "AccountType",
                table: "Patients",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "AtsiStatus",
                table: "Patients",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "BusinessHoursPhone",
                table: "Patients",
                type: "TEXT",
                maxLength: 20,
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "CtgCoPaymentRelief",
                table: "Patients",
                type: "INTEGER",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "Deceased",
                table: "Patients",
                type: "INTEGER",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<int>(
                name: "DobAccuracy",
                table: "Patients",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<DateTime>(
                name: "DvaExpiryDate",
                table: "Patients",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "EmergencyContactPatientId",
                table: "Patients",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "EntitlementStatus",
                table: "Patients",
                type: "TEXT",
                maxLength: 50,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Ethnicity",
                table: "Patients",
                type: "TEXT",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "FaxNumber",
                table: "Patients",
                type: "TEXT",
                maxLength: 20,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "FeeRateCode",
                table: "Patients",
                type: "TEXT",
                maxLength: 20,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "FileNumber",
                table: "Patients",
                type: "TEXT",
                maxLength: 50,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "Gender",
                table: "Patients",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "HealthFundAliasFamily",
                table: "Patients",
                type: "TEXT",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "HealthFundAliasFirst",
                table: "Patients",
                type: "TEXT",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "HealthFundCode",
                table: "Patients",
                type: "TEXT",
                maxLength: 20,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "HealthFundRef",
                table: "Patients",
                type: "TEXT",
                maxLength: 50,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "IhiAssignedDate",
                table: "Patients",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "IhiNumber",
                table: "Patients",
                type: "TEXT",
                maxLength: 16,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "IhiNumberStatus",
                table: "Patients",
                type: "TEXT",
                maxLength: 50,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "IhiRecordStatus",
                table: "Patients",
                type: "TEXT",
                maxLength: 50,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "IhiUnresolvedDate",
                table: "Patients",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "InterpreterLanguage",
                table: "Patients",
                type: "TEXT",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "InterpreterRequired",
                table: "Patients",
                type: "INTEGER",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<DateTime>(
                name: "LastSeenDate",
                table: "Patients",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "LifeCardNum",
                table: "Patients",
                type: "TEXT",
                maxLength: 50,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "MaidenName",
                table: "Patients",
                type: "TEXT",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "MaritalStatus",
                table: "Patients",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "MedicareExpiryDate",
                table: "Patients",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "MedicareIncentiveEligible",
                table: "Patients",
                type: "INTEGER",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "MiddleName",
                table: "Patients",
                type: "TEXT",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "MobilePhone",
                table: "Patients",
                type: "TEXT",
                maxLength: 20,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "NextOfKinName",
                table: "Patients",
                type: "TEXT",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "NextOfKinPatientId",
                table: "Patients",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "NextOfKinPhone",
                table: "Patients",
                type: "TEXT",
                maxLength: 20,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PayerName",
                table: "Patients",
                type: "TEXT",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "PayerPatientId",
                table: "Patients",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "PensionExpiryDate",
                table: "Patients",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PlaceOfBirth",
                table: "Patients",
                type: "TEXT",
                maxLength: 255,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PreferredName",
                table: "Patients",
                type: "TEXT",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "ProviderId",
                table: "Patients",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "SafetyNetNumber",
                table: "Patients",
                type: "TEXT",
                maxLength: 50,
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "SameAsNextOfKin",
                table: "Patients",
                type: "INTEGER",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "Title",
                table: "Patients",
                type: "TEXT",
                maxLength: 20,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "UrNumber",
                table: "Patients",
                type: "TEXT",
                maxLength: 50,
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "UseMedicareRegisteredBankAccount",
                table: "Patients",
                type: "INTEGER",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "Warnings",
                table: "Patients",
                type: "TEXT",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "PatientAddresses",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    PatientId = table.Column<int>(type: "INTEGER", nullable: false),
                    AddressType = table.Column<int>(type: "INTEGER", nullable: false),
                    AddressLine1 = table.Column<string>(type: "TEXT", maxLength: 255, nullable: false),
                    AddressLine2 = table.Column<string>(type: "TEXT", maxLength: 255, nullable: true),
                    Suburb = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    Postcode = table.Column<string>(type: "TEXT", maxLength: 10, nullable: false),
                    State = table.Column<string>(type: "TEXT", maxLength: 10, nullable: false),
                    AddressSubType = table.Column<string>(type: "TEXT", maxLength: 50, nullable: false),
                    Community = table.Column<string>(type: "TEXT", maxLength: 100, nullable: true),
                    SendToMedicare = table.Column<bool>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PatientAddresses", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PatientAddresses_Patients_PatientId",
                        column: x => x.PatientId,
                        principalTable: "Patients",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "PatientCompensationClaims",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    PatientId = table.Column<int>(type: "INTEGER", nullable: false),
                    ClaimNum = table.Column<string>(type: "TEXT", maxLength: 30, nullable: false),
                    DateOfInjury = table.Column<DateTime>(type: "TEXT", nullable: true),
                    EmployerName = table.Column<string>(type: "TEXT", maxLength: 200, nullable: true),
                    CaseManagerName = table.Column<string>(type: "TEXT", maxLength: 200, nullable: true),
                    PayerName = table.Column<string>(type: "TEXT", maxLength: 200, nullable: true),
                    IsDefault = table.Column<bool>(type: "INTEGER", nullable: false),
                    PublicNote = table.Column<string>(type: "TEXT", maxLength: 500, nullable: true),
                    PrivateNote = table.Column<string>(type: "TEXT", maxLength: 500, nullable: true),
                    IsArchived = table.Column<bool>(type: "INTEGER", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PatientCompensationClaims", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PatientCompensationClaims_Patients_PatientId",
                        column: x => x.PatientId,
                        principalTable: "Patients",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "PatientFamilyRelationships",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    PatientId = table.Column<int>(type: "INTEGER", nullable: false),
                    RelativePatientId = table.Column<int>(type: "INTEGER", nullable: false),
                    RelationshipType = table.Column<string>(type: "TEXT", maxLength: 50, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PatientFamilyRelationships", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PatientFamilyRelationships_Patients_PatientId",
                        column: x => x.PatientId,
                        principalTable: "Patients",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_PatientFamilyRelationships_Patients_RelativePatientId",
                        column: x => x.RelativePatientId,
                        principalTable: "Patients",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "PatientOccupations",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    PatientId = table.Column<int>(type: "INTEGER", nullable: false),
                    Occupation = table.Column<string>(type: "TEXT", maxLength: 255, nullable: true),
                    Employer = table.Column<string>(type: "TEXT", maxLength: 255, nullable: true),
                    StartedYear = table.Column<int>(type: "INTEGER", nullable: true),
                    StoppedYear = table.Column<int>(type: "INTEGER", nullable: true),
                    HasAsbestos = table.Column<bool>(type: "INTEGER", nullable: false),
                    HasDust = table.Column<bool>(type: "INTEGER", nullable: false),
                    HasRadiation = table.Column<bool>(type: "INTEGER", nullable: false),
                    HasAnimals = table.Column<bool>(type: "INTEGER", nullable: false),
                    Comment = table.Column<string>(type: "TEXT", maxLength: 255, nullable: true),
                    IsArchived = table.Column<bool>(type: "INTEGER", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PatientOccupations", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PatientOccupations_Patients_PatientId",
                        column: x => x.PatientId,
                        principalTable: "Patients",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "PatientReferrals",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    PatientId = table.Column<int>(type: "INTEGER", nullable: false),
                    ReferralDate = table.Column<DateTime>(type: "TEXT", nullable: false),
                    ReferralPeriod = table.Column<string>(type: "TEXT", maxLength: 2, nullable: true),
                    IsDefault = table.Column<bool>(type: "INTEGER", nullable: false),
                    IsGP = table.Column<bool>(type: "INTEGER", nullable: false),
                    ReferringProviderName = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
                    ReferringProviderNumber = table.Column<string>(type: "TEXT", maxLength: 20, nullable: true),
                    RequestTypeCde = table.Column<string>(type: "TEXT", maxLength: 1, nullable: true),
                    FirstVisitDate = table.Column<DateTime>(type: "TEXT", nullable: true),
                    Note = table.Column<string>(type: "TEXT", maxLength: 255, nullable: true),
                    IsArchived = table.Column<bool>(type: "INTEGER", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PatientReferrals", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PatientReferrals_Patients_PatientId",
                        column: x => x.PatientId,
                        principalTable: "Patients",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "UserDefinedFieldTypes",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Name = table.Column<string>(type: "TEXT", maxLength: 50, nullable: false),
                    FieldType = table.Column<int>(type: "INTEGER", nullable: false),
                    DefaultValue = table.Column<string>(type: "TEXT", maxLength: 255, nullable: true),
                    DisplayOrder = table.Column<int>(type: "INTEGER", nullable: false),
                    IsActive = table.Column<bool>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UserDefinedFieldTypes", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "PatientUserDefinedFieldValues",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    PatientId = table.Column<int>(type: "INTEGER", nullable: false),
                    UserDefinedFieldTypeId = table.Column<int>(type: "INTEGER", nullable: false),
                    Value = table.Column<string>(type: "TEXT", maxLength: 255, nullable: true),
                    Note = table.Column<string>(type: "TEXT", maxLength: 255, nullable: true),
                    IsDefault = table.Column<bool>(type: "INTEGER", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PatientUserDefinedFieldValues", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PatientUserDefinedFieldValues_Patients_PatientId",
                        column: x => x.PatientId,
                        principalTable: "Patients",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_PatientUserDefinedFieldValues_UserDefinedFieldTypes_UserDefinedFieldTypeId",
                        column: x => x.UserDefinedFieldTypeId,
                        principalTable: "UserDefinedFieldTypes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Patients_EmergencyContactPatientId",
                table: "Patients",
                column: "EmergencyContactPatientId");

            migrationBuilder.CreateIndex(
                name: "IX_Patients_NextOfKinPatientId",
                table: "Patients",
                column: "NextOfKinPatientId");

            migrationBuilder.CreateIndex(
                name: "IX_Patients_PayerPatientId",
                table: "Patients",
                column: "PayerPatientId");

            migrationBuilder.CreateIndex(
                name: "IX_Patients_ProviderId",
                table: "Patients",
                column: "ProviderId");

            migrationBuilder.CreateIndex(
                name: "IX_PatientAddresses_PatientId_AddressType",
                table: "PatientAddresses",
                columns: new[] { "PatientId", "AddressType" });

            migrationBuilder.CreateIndex(
                name: "IX_PatientCompensationClaims_PatientId",
                table: "PatientCompensationClaims",
                column: "PatientId");

            migrationBuilder.CreateIndex(
                name: "IX_PatientFamilyRelationships_PatientId",
                table: "PatientFamilyRelationships",
                column: "PatientId");

            migrationBuilder.CreateIndex(
                name: "IX_PatientFamilyRelationships_RelativePatientId",
                table: "PatientFamilyRelationships",
                column: "RelativePatientId");

            migrationBuilder.CreateIndex(
                name: "IX_PatientOccupations_PatientId",
                table: "PatientOccupations",
                column: "PatientId");

            migrationBuilder.CreateIndex(
                name: "IX_PatientReferrals_PatientId",
                table: "PatientReferrals",
                column: "PatientId");

            migrationBuilder.CreateIndex(
                name: "IX_PatientUserDefinedFieldValues_PatientId",
                table: "PatientUserDefinedFieldValues",
                column: "PatientId");

            migrationBuilder.CreateIndex(
                name: "IX_PatientUserDefinedFieldValues_UserDefinedFieldTypeId",
                table: "PatientUserDefinedFieldValues",
                column: "UserDefinedFieldTypeId");

            migrationBuilder.CreateIndex(
                name: "IX_UserDefinedFieldTypes_Name",
                table: "UserDefinedFieldTypes",
                column: "Name",
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_Patients_Patients_EmergencyContactPatientId",
                table: "Patients",
                column: "EmergencyContactPatientId",
                principalTable: "Patients",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Patients_Patients_NextOfKinPatientId",
                table: "Patients",
                column: "NextOfKinPatientId",
                principalTable: "Patients",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Patients_Patients_PayerPatientId",
                table: "Patients",
                column: "PayerPatientId",
                principalTable: "Patients",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Patients_Practitioners_ProviderId",
                table: "Patients",
                column: "ProviderId",
                principalTable: "Practitioners",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Patients_Patients_EmergencyContactPatientId",
                table: "Patients");

            migrationBuilder.DropForeignKey(
                name: "FK_Patients_Patients_NextOfKinPatientId",
                table: "Patients");

            migrationBuilder.DropForeignKey(
                name: "FK_Patients_Patients_PayerPatientId",
                table: "Patients");

            migrationBuilder.DropForeignKey(
                name: "FK_Patients_Practitioners_ProviderId",
                table: "Patients");

            migrationBuilder.DropTable(
                name: "PatientAddresses");

            migrationBuilder.DropTable(
                name: "PatientCompensationClaims");

            migrationBuilder.DropTable(
                name: "PatientFamilyRelationships");

            migrationBuilder.DropTable(
                name: "PatientOccupations");

            migrationBuilder.DropTable(
                name: "PatientReferrals");

            migrationBuilder.DropTable(
                name: "PatientUserDefinedFieldValues");

            migrationBuilder.DropTable(
                name: "UserDefinedFieldTypes");

            migrationBuilder.DropIndex(
                name: "IX_Patients_EmergencyContactPatientId",
                table: "Patients");

            migrationBuilder.DropIndex(
                name: "IX_Patients_NextOfKinPatientId",
                table: "Patients");

            migrationBuilder.DropIndex(
                name: "IX_Patients_PayerPatientId",
                table: "Patients");

            migrationBuilder.DropIndex(
                name: "IX_Patients_ProviderId",
                table: "Patients");

            migrationBuilder.DropColumn(
                name: "AcceptEmail",
                table: "Patients");

            migrationBuilder.DropColumn(
                name: "AcceptOnlineAppointments",
                table: "Patients");

            migrationBuilder.DropColumn(
                name: "AcceptSms",
                table: "Patients");

            migrationBuilder.DropColumn(
                name: "AcceptSmsMarketing",
                table: "Patients");

            migrationBuilder.DropColumn(
                name: "AccountBsb",
                table: "Patients");

            migrationBuilder.DropColumn(
                name: "AccountName",
                table: "Patients");

            migrationBuilder.DropColumn(
                name: "AccountNumber",
                table: "Patients");

            migrationBuilder.DropColumn(
                name: "AccountType",
                table: "Patients");

            migrationBuilder.DropColumn(
                name: "AtsiStatus",
                table: "Patients");

            migrationBuilder.DropColumn(
                name: "BusinessHoursPhone",
                table: "Patients");

            migrationBuilder.DropColumn(
                name: "CtgCoPaymentRelief",
                table: "Patients");

            migrationBuilder.DropColumn(
                name: "Deceased",
                table: "Patients");

            migrationBuilder.DropColumn(
                name: "DobAccuracy",
                table: "Patients");

            migrationBuilder.DropColumn(
                name: "DvaExpiryDate",
                table: "Patients");

            migrationBuilder.DropColumn(
                name: "EmergencyContactPatientId",
                table: "Patients");

            migrationBuilder.DropColumn(
                name: "EntitlementStatus",
                table: "Patients");

            migrationBuilder.DropColumn(
                name: "Ethnicity",
                table: "Patients");

            migrationBuilder.DropColumn(
                name: "FaxNumber",
                table: "Patients");

            migrationBuilder.DropColumn(
                name: "FeeRateCode",
                table: "Patients");

            migrationBuilder.DropColumn(
                name: "FileNumber",
                table: "Patients");

            migrationBuilder.DropColumn(
                name: "Gender",
                table: "Patients");

            migrationBuilder.DropColumn(
                name: "HealthFundAliasFamily",
                table: "Patients");

            migrationBuilder.DropColumn(
                name: "HealthFundAliasFirst",
                table: "Patients");

            migrationBuilder.DropColumn(
                name: "HealthFundCode",
                table: "Patients");

            migrationBuilder.DropColumn(
                name: "HealthFundRef",
                table: "Patients");

            migrationBuilder.DropColumn(
                name: "IhiAssignedDate",
                table: "Patients");

            migrationBuilder.DropColumn(
                name: "IhiNumber",
                table: "Patients");

            migrationBuilder.DropColumn(
                name: "IhiNumberStatus",
                table: "Patients");

            migrationBuilder.DropColumn(
                name: "IhiRecordStatus",
                table: "Patients");

            migrationBuilder.DropColumn(
                name: "IhiUnresolvedDate",
                table: "Patients");

            migrationBuilder.DropColumn(
                name: "InterpreterLanguage",
                table: "Patients");

            migrationBuilder.DropColumn(
                name: "InterpreterRequired",
                table: "Patients");

            migrationBuilder.DropColumn(
                name: "LastSeenDate",
                table: "Patients");

            migrationBuilder.DropColumn(
                name: "LifeCardNum",
                table: "Patients");

            migrationBuilder.DropColumn(
                name: "MaidenName",
                table: "Patients");

            migrationBuilder.DropColumn(
                name: "MaritalStatus",
                table: "Patients");

            migrationBuilder.DropColumn(
                name: "MedicareExpiryDate",
                table: "Patients");

            migrationBuilder.DropColumn(
                name: "MedicareIncentiveEligible",
                table: "Patients");

            migrationBuilder.DropColumn(
                name: "MiddleName",
                table: "Patients");

            migrationBuilder.DropColumn(
                name: "MobilePhone",
                table: "Patients");

            migrationBuilder.DropColumn(
                name: "NextOfKinName",
                table: "Patients");

            migrationBuilder.DropColumn(
                name: "NextOfKinPatientId",
                table: "Patients");

            migrationBuilder.DropColumn(
                name: "NextOfKinPhone",
                table: "Patients");

            migrationBuilder.DropColumn(
                name: "PayerName",
                table: "Patients");

            migrationBuilder.DropColumn(
                name: "PayerPatientId",
                table: "Patients");

            migrationBuilder.DropColumn(
                name: "PensionExpiryDate",
                table: "Patients");

            migrationBuilder.DropColumn(
                name: "PlaceOfBirth",
                table: "Patients");

            migrationBuilder.DropColumn(
                name: "PreferredName",
                table: "Patients");

            migrationBuilder.DropColumn(
                name: "ProviderId",
                table: "Patients");

            migrationBuilder.DropColumn(
                name: "SafetyNetNumber",
                table: "Patients");

            migrationBuilder.DropColumn(
                name: "SameAsNextOfKin",
                table: "Patients");

            migrationBuilder.DropColumn(
                name: "Title",
                table: "Patients");

            migrationBuilder.DropColumn(
                name: "UrNumber",
                table: "Patients");

            migrationBuilder.DropColumn(
                name: "UseMedicareRegisteredBankAccount",
                table: "Patients");

            migrationBuilder.DropColumn(
                name: "Warnings",
                table: "Patients");
        }
    }
}
