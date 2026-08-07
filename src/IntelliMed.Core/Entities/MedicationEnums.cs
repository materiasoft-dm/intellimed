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

/// <summary>PBS/RPBS subsidy-listing restriction category. Applies to a listing, not the drug itself
/// — only meaningful when Medicine.IsPbsListed or IsRpbsListed is true, hence nullable. Deliberately
/// collapsed to the three prescriber-facing categories (not split into Streamlined vs phone/written
/// Authority Required, which share the same "you must get sign-off first" implication for a
/// prescribing workflow) — that split can be added later if it turns out to matter.</summary>
public enum PbsRestrictionLevel
{
    Unrestricted,
    Restricted,
    AuthorityRequired
}
