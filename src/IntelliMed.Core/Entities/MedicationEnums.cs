namespace IntelliMed.Core.Entities;

/// <summary>Poisons Standard (SUSMP) scheduling. Only the categories relevant to prescribable/
/// dispensable medicines are modeled — S5/S6/S7/S9 cover industrial/agricultural/illicit substances,
/// not medicines. A real product's schedule can vary by pack size/strength/indication; this is a
/// single per-catalog-entry classification, not a substitute for checking the current Poisons Standard.</summary>
public enum MedicineSchedule
{
    Unscheduled,
    S2,
    S3,
    S4,
    S8
}

/// <summary>Provenance of a Medicine row — governs whether it can be edited/deactivated manually.
/// Set once at creation and never changed afterward: Manual by MedicineRepository.CreateAsync,
/// Synced by any import/sync service (today just MedicineImportService's CSV import; the same value
/// will cover a future AMT/PBS/ARTG sync too, so this doesn't need to change again for that).</summary>
public enum MedicineSource
{
    Manual,
    Synced
}
