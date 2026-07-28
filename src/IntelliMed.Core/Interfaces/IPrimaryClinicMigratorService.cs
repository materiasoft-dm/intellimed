using IntelliMed.Core.DTOs;
using System.Text.Json;

namespace IntelliMed.Core.Interfaces;

/// <summary>
/// Upserts legacy Pracnet rows into the Client/Invoice model, matching existing records by LegacyGuid
/// so re-importing the same rows updates in place instead of creating duplicates. Rows are read from
/// the legacy SQL Server database and shipped here as JSON by the IntelliMed.PrimaryClinicMigrator
/// WinForms tool — this API never talks to SQL Server directly.
/// </summary>
public interface IPrimaryClinicMigratorService
{
    Task<PrimaryClinicMigratorResultDto> ImportClientsAsync(List<Dictionary<string, JsonElement>> rows, int clinicId);
    Task<PrimaryClinicMigratorResultDto> ImportAddressesAsync(List<Dictionary<string, JsonElement>> rows);
    Task<PrimaryClinicMigratorResultDto> ImportReferralsAsync(List<Dictionary<string, JsonElement>> rows);
    Task<PrimaryClinicMigratorResultDto> ImportOccupationsAsync(List<Dictionary<string, JsonElement>> rows);
    Task<PrimaryClinicMigratorResultDto> ImportFamilyRelationshipsAsync(List<Dictionary<string, JsonElement>> rows);
    Task<PrimaryClinicMigratorResultDto> ImportCompensationClaimsAsync(List<Dictionary<string, JsonElement>> rows);
    Task<PrimaryClinicMigratorResultDto> ImportInvoicesAsync(List<Dictionary<string, JsonElement>> rows);
    Task<PrimaryClinicMigratorResultDto> ImportInvoiceItemsAsync(List<Dictionary<string, JsonElement>> rows);
    Task<PrimaryClinicMigratorResultDto> ImportPaymentsAsync(List<Dictionary<string, JsonElement>> allocationRows, List<Dictionary<string, JsonElement>> paymentRows);
}
