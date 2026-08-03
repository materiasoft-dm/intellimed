namespace IntelliMed.Core.Entities;

public enum ClientTypeEnum
{
    Person,
    Organisation
}

public enum GenderEnum
{
    Unspecified,
    Male,
    Female,
    Other
}

public enum DobAccuracyEnum
{
    Day,
    Month,
    Year,
    Estimated
}

public enum MaritalStatusEnum
{
    Unknown,
    Single,
    Married,
    DeFacto,
    Divorced,
    Widowed,
    Separated
}

public enum AtsiStatusEnum
{
    NotAsked,
    AboriginalOnly,
    TorresStraitIslanderOnly,
    Both,
    NeitherAboriginalNorTorresStraitIslander
}

public enum AccountTypeEnum
{
    PrivatePatient,
    Concession,
    Pensioner,
    Veteran,
    WorkCover,
    Tac,
    BulkBill,
    Other,
    /// <summary>In-Patient Medical Claim — resolves via the same fund-matched fee schedule as PrivatePatient (see BillingCalculator.ResolveChargedFeeScheduleIdAsync), differs only in claiming path.</summary>
    Imc,
    /// <summary>Overseas Visitor claim (reciprocal health care agreement / overseas student cover /
    /// self-funded). Resolves via the same fund-matched fee schedule cascade as PrivatePatient/Imc —
    /// no BillingCalculator special-casing needed.</summary>
    Ovs,
    /// <summary>Child immunisation. Legacy's code constant is AccountType_ChildImm but its actual DB
    /// Name is "Immunisation" — that mismatch is intentional here too (member name vs. display label).
    /// A normal manually-billable account type through the generic BillingCalculator cascade; legacy's
    /// auto-invoice-from-immunisation-encounter behavior and its separate AIR/ACIR claiming path are
    /// out of scope — there's no immunisation-encounter feature here to hang either off of.</summary>
    ChildImm
}
