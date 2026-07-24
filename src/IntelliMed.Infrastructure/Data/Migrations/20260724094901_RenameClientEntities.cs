using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace IntelliMed.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class RenameClientEntities : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Table renames (non-destructive — preserves all existing rows).
            migrationBuilder.RenameTable(name: "Patients", newName: "Clients");
            migrationBuilder.RenameTable(name: "PatientAddresses", newName: "ClientAddresses");
            migrationBuilder.RenameTable(name: "PatientCompensationClaims", newName: "ClientCompensationClaims");
            migrationBuilder.RenameTable(name: "PatientFamilyRelationships", newName: "ClientFamilyRelationships");
            migrationBuilder.RenameTable(name: "PatientOccupations", newName: "ClientOccupations");
            migrationBuilder.RenameTable(name: "PatientReferrals", newName: "ClientReferrals");
            migrationBuilder.RenameTable(name: "PatientUserDefinedFieldValues", newName: "ClientUserDefinedFieldValues");

            // Column renames (table names below are the new, post-rename names).
            migrationBuilder.RenameColumn(name: "NextOfKinPatientId", table: "Clients", newName: "NextOfKinClientId");
            migrationBuilder.RenameColumn(name: "EmergencyContactPatientId", table: "Clients", newName: "EmergencyContactClientId");
            migrationBuilder.RenameColumn(name: "PayerPatientId", table: "Clients", newName: "PayerClientId");
            migrationBuilder.RenameColumn(name: "PatientId", table: "ClientAddresses", newName: "ClientId");
            migrationBuilder.RenameColumn(name: "PatientId", table: "ClientCompensationClaims", newName: "ClientId");
            migrationBuilder.RenameColumn(name: "PatientId", table: "ClientFamilyRelationships", newName: "ClientId");
            migrationBuilder.RenameColumn(name: "RelativePatientId", table: "ClientFamilyRelationships", newName: "RelativeClientId");
            migrationBuilder.RenameColumn(name: "PatientId", table: "ClientOccupations", newName: "ClientId");
            migrationBuilder.RenameColumn(name: "PatientId", table: "ClientReferrals", newName: "ClientId");
            migrationBuilder.RenameColumn(name: "PatientId", table: "ClientUserDefinedFieldValues", newName: "ClientId");
            migrationBuilder.RenameColumn(name: "PatientId", table: "Appointments", newName: "ClientId");
            migrationBuilder.RenameColumn(name: "PatientId", table: "Invoices", newName: "ClientId");

            // Index renames (cosmetic, but kept in sync with EF's naming convention).
            migrationBuilder.RenameIndex(name: "IX_Patients_ClinicId", table: "Clients", newName: "IX_Clients_ClinicId");
            migrationBuilder.RenameIndex(name: "IX_Patients_EmergencyContactPatientId", table: "Clients", newName: "IX_Clients_EmergencyContactClientId");
            migrationBuilder.RenameIndex(name: "IX_Patients_HealthFundId", table: "Clients", newName: "IX_Clients_HealthFundId");
            migrationBuilder.RenameIndex(name: "IX_Patients_LastName_FirstName", table: "Clients", newName: "IX_Clients_LastName_FirstName");
            migrationBuilder.RenameIndex(name: "IX_Patients_MedicareNumber", table: "Clients", newName: "IX_Clients_MedicareNumber");
            migrationBuilder.RenameIndex(name: "IX_Patients_NextOfKinPatientId", table: "Clients", newName: "IX_Clients_NextOfKinClientId");
            migrationBuilder.RenameIndex(name: "IX_Patients_PayerPatientId", table: "Clients", newName: "IX_Clients_PayerClientId");
            migrationBuilder.RenameIndex(name: "IX_Patients_ProviderId", table: "Clients", newName: "IX_Clients_ProviderId");
            migrationBuilder.RenameIndex(name: "IX_PatientAddresses_PatientId_AddressType", table: "ClientAddresses", newName: "IX_ClientAddresses_ClientId_AddressType");
            migrationBuilder.RenameIndex(name: "IX_PatientCompensationClaims_PatientId", table: "ClientCompensationClaims", newName: "IX_ClientCompensationClaims_ClientId");
            migrationBuilder.RenameIndex(name: "IX_PatientFamilyRelationships_PatientId", table: "ClientFamilyRelationships", newName: "IX_ClientFamilyRelationships_ClientId");
            migrationBuilder.RenameIndex(name: "IX_PatientFamilyRelationships_RelativePatientId", table: "ClientFamilyRelationships", newName: "IX_ClientFamilyRelationships_RelativeClientId");
            migrationBuilder.RenameIndex(name: "IX_PatientOccupations_PatientId", table: "ClientOccupations", newName: "IX_ClientOccupations_ClientId");
            migrationBuilder.RenameIndex(name: "IX_PatientReferrals_PatientId", table: "ClientReferrals", newName: "IX_ClientReferrals_ClientId");
            migrationBuilder.RenameIndex(name: "IX_PatientUserDefinedFieldValues_PatientId", table: "ClientUserDefinedFieldValues", newName: "IX_ClientUserDefinedFieldValues_ClientId");
            migrationBuilder.RenameIndex(name: "IX_PatientUserDefinedFieldValues_UserDefinedFieldTypeId", table: "ClientUserDefinedFieldValues", newName: "IX_ClientUserDefinedFieldValues_UserDefinedFieldTypeId");
            migrationBuilder.RenameIndex(name: "IX_Appointments_PatientId", table: "Appointments", newName: "IX_Appointments_ClientId");
            migrationBuilder.RenameIndex(name: "IX_Invoices_PatientId", table: "Invoices", newName: "IX_Invoices_ClientId");

            // RolePermissions rows were seeded once (seeding is a no-op if the table is already
            // populated), so already-granted permissions must be data-fixed to the renamed page
            // keys or they'll silently stop matching RolePermissionsController.GetCategoryForPage.
            migrationBuilder.Sql("UPDATE RolePermissions SET PageKey = 'clients' WHERE PageKey = 'patients';");
            migrationBuilder.Sql("UPDATE RolePermissions SET PageKey = 'clients/create' WHERE PageKey = 'patients/create';");
            migrationBuilder.Sql("UPDATE RolePermissions SET PageKey = 'clients/edit' WHERE PageKey = 'patients/edit';");
            migrationBuilder.Sql("UPDATE RolePermissions SET PageKey = 'clients/delete' WHERE PageKey = 'patients/delete';");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("UPDATE RolePermissions SET PageKey = 'patients' WHERE PageKey = 'clients';");
            migrationBuilder.Sql("UPDATE RolePermissions SET PageKey = 'patients/create' WHERE PageKey = 'clients/create';");
            migrationBuilder.Sql("UPDATE RolePermissions SET PageKey = 'patients/edit' WHERE PageKey = 'clients/edit';");
            migrationBuilder.Sql("UPDATE RolePermissions SET PageKey = 'patients/delete' WHERE PageKey = 'clients/delete';");

            migrationBuilder.RenameIndex(name: "IX_Invoices_ClientId", table: "Invoices", newName: "IX_Invoices_PatientId");
            migrationBuilder.RenameIndex(name: "IX_Appointments_ClientId", table: "Appointments", newName: "IX_Appointments_PatientId");
            migrationBuilder.RenameIndex(name: "IX_ClientUserDefinedFieldValues_UserDefinedFieldTypeId", table: "ClientUserDefinedFieldValues", newName: "IX_PatientUserDefinedFieldValues_UserDefinedFieldTypeId");
            migrationBuilder.RenameIndex(name: "IX_ClientUserDefinedFieldValues_ClientId", table: "ClientUserDefinedFieldValues", newName: "IX_PatientUserDefinedFieldValues_PatientId");
            migrationBuilder.RenameIndex(name: "IX_ClientReferrals_ClientId", table: "ClientReferrals", newName: "IX_PatientReferrals_PatientId");
            migrationBuilder.RenameIndex(name: "IX_ClientOccupations_ClientId", table: "ClientOccupations", newName: "IX_PatientOccupations_PatientId");
            migrationBuilder.RenameIndex(name: "IX_ClientFamilyRelationships_RelativeClientId", table: "ClientFamilyRelationships", newName: "IX_PatientFamilyRelationships_RelativePatientId");
            migrationBuilder.RenameIndex(name: "IX_ClientFamilyRelationships_ClientId", table: "ClientFamilyRelationships", newName: "IX_PatientFamilyRelationships_PatientId");
            migrationBuilder.RenameIndex(name: "IX_ClientCompensationClaims_ClientId", table: "ClientCompensationClaims", newName: "IX_PatientCompensationClaims_PatientId");
            migrationBuilder.RenameIndex(name: "IX_ClientAddresses_ClientId_AddressType", table: "ClientAddresses", newName: "IX_PatientAddresses_PatientId_AddressType");
            migrationBuilder.RenameIndex(name: "IX_Clients_ProviderId", table: "Clients", newName: "IX_Patients_ProviderId");
            migrationBuilder.RenameIndex(name: "IX_Clients_PayerClientId", table: "Clients", newName: "IX_Patients_PayerPatientId");
            migrationBuilder.RenameIndex(name: "IX_Clients_NextOfKinClientId", table: "Clients", newName: "IX_Patients_NextOfKinPatientId");
            migrationBuilder.RenameIndex(name: "IX_Clients_MedicareNumber", table: "Clients", newName: "IX_Patients_MedicareNumber");
            migrationBuilder.RenameIndex(name: "IX_Clients_LastName_FirstName", table: "Clients", newName: "IX_Patients_LastName_FirstName");
            migrationBuilder.RenameIndex(name: "IX_Clients_HealthFundId", table: "Clients", newName: "IX_Patients_HealthFundId");
            migrationBuilder.RenameIndex(name: "IX_Clients_EmergencyContactClientId", table: "Clients", newName: "IX_Patients_EmergencyContactPatientId");
            migrationBuilder.RenameIndex(name: "IX_Clients_ClinicId", table: "Clients", newName: "IX_Patients_ClinicId");

            migrationBuilder.RenameColumn(name: "ClientId", table: "Invoices", newName: "PatientId");
            migrationBuilder.RenameColumn(name: "ClientId", table: "Appointments", newName: "PatientId");
            migrationBuilder.RenameColumn(name: "ClientId", table: "ClientUserDefinedFieldValues", newName: "PatientId");
            migrationBuilder.RenameColumn(name: "ClientId", table: "ClientReferrals", newName: "PatientId");
            migrationBuilder.RenameColumn(name: "ClientId", table: "ClientOccupations", newName: "PatientId");
            migrationBuilder.RenameColumn(name: "RelativeClientId", table: "ClientFamilyRelationships", newName: "RelativePatientId");
            migrationBuilder.RenameColumn(name: "ClientId", table: "ClientFamilyRelationships", newName: "PatientId");
            migrationBuilder.RenameColumn(name: "ClientId", table: "ClientCompensationClaims", newName: "PatientId");
            migrationBuilder.RenameColumn(name: "ClientId", table: "ClientAddresses", newName: "PatientId");
            migrationBuilder.RenameColumn(name: "PayerClientId", table: "Clients", newName: "PayerPatientId");
            migrationBuilder.RenameColumn(name: "EmergencyContactClientId", table: "Clients", newName: "EmergencyContactPatientId");
            migrationBuilder.RenameColumn(name: "NextOfKinClientId", table: "Clients", newName: "NextOfKinPatientId");

            migrationBuilder.RenameTable(name: "ClientUserDefinedFieldValues", newName: "PatientUserDefinedFieldValues");
            migrationBuilder.RenameTable(name: "ClientReferrals", newName: "PatientReferrals");
            migrationBuilder.RenameTable(name: "ClientOccupations", newName: "PatientOccupations");
            migrationBuilder.RenameTable(name: "ClientFamilyRelationships", newName: "PatientFamilyRelationships");
            migrationBuilder.RenameTable(name: "ClientCompensationClaims", newName: "PatientCompensationClaims");
            migrationBuilder.RenameTable(name: "ClientAddresses", newName: "PatientAddresses");
            migrationBuilder.RenameTable(name: "Clients", newName: "Patients");
        }
    }
}
