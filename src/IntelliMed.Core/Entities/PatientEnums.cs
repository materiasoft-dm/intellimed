namespace IntelliMed.Core.Entities;

public enum PatientTypeEnum
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
    Other
}
