using IntelliMed.Core.Entities;

namespace IntelliMed.Infrastructure.Services;

/// <summary>
/// Pure field-translation logic for the PrimaryClinic Migrator, extracted from
/// PrimaryClinicMigratorService so it's testable without a live SQL Server connection.
/// </summary>
public static class PrimaryClinicLegacyMapper
{
    public static readonly IReadOnlyDictionary<string, AccountTypeEnum> AccountTypeNameMap = new Dictionary<string, AccountTypeEnum>(StringComparer.OrdinalIgnoreCase)
    {
        ["Private Patient"] = AccountTypeEnum.PrivatePatient,
        ["DVA"] = AccountTypeEnum.Veteran,
        ["WorkCover"] = AccountTypeEnum.WorkCover,
        ["TAC"] = AccountTypeEnum.Tac,
        ["IMC"] = AccountTypeEnum.Imc,
        ["BB"] = AccountTypeEnum.BulkBill,
        // "Health Fund" is expressed via Client.HealthFundId in our model, not a distinct account type.
        ["Health Fund"] = AccountTypeEnum.PrivatePatient,
        ["OVS"] = AccountTypeEnum.Ovs,
        ["Immunisation"] = AccountTypeEnum.ChildImm,
        ["Others"] = AccountTypeEnum.Other,
        ["No Charge"] = AccountTypeEnum.Other,
        ["Credit Note"] = AccountTypeEnum.Other,
    };

    public static AccountTypeEnum MapAccountType(string? accountTypeName) =>
        AccountTypeNameMap.GetValueOrDefault(accountTypeName ?? string.Empty, AccountTypeEnum.Other);

    public static readonly IReadOnlyDictionary<string, PaymentMethod> PaymentTypeNameMap = new Dictionary<string, PaymentMethod>(StringComparer.OrdinalIgnoreCase)
    {
        ["Cash"] = PaymentMethod.Cash,
        ["Cheque"] = PaymentMethod.Cheque,
        ["Debit Card"] = PaymentMethod.Eftpos,
        ["Visa"] = PaymentMethod.CreditCard,
        ["Master"] = PaymentMethod.CreditCard,
        ["AMEX"] = PaymentMethod.CreditCard,
        ["Diners"] = PaymentMethod.CreditCard,
        ["Direct Credit"] = PaymentMethod.BankTransfer,
        // "Credit allocation"/"Discount"/"Write off" are account adjustments rather than real tender
        // types — legacy still nets them against the invoice balance, so we import them as Payment
        // rows (to keep AmountPaid arithmetic consistent) but there's no matching PaymentMethod, so
        // they fall through to Other along with "Others" itself.
        ["Others"] = PaymentMethod.Other,
        ["Credit allocation"] = PaymentMethod.Other,
        ["Discount"] = PaymentMethod.Other,
        ["Write off"] = PaymentMethod.Other,
    };

    public static PaymentMethod MapPaymentMethod(string? paymentTypeName) =>
        PaymentTypeNameMap.GetValueOrDefault(paymentTypeName ?? string.Empty, PaymentMethod.Other);

    /// <summary>Legacy tracks Hospital/Rooms per invoice *item* (InvoiceItems.HospitalInd); our model
    /// tracks it per invoice — if any line was a hospital service, the whole invoice is classed Hospital.</summary>
    public static PlaceOfServiceEnum MapPlaceOfService(bool anyItemHospitalInd) =>
        anyItemHospitalInd ? PlaceOfServiceEnum.Hospital : PlaceOfServiceEnum.Rooms;

    public static GenderEnum MapGender(string? code) => code?.Trim().ToUpperInvariant() switch
    {
        "M" => GenderEnum.Male,
        "F" => GenderEnum.Female,
        _ => GenderEnum.Unspecified
    };

    public static DobAccuracyEnum MapDobAccuracy(string? code) => code?.Trim().ToUpperInvariant() switch
    {
        "Y" => DobAccuracyEnum.Year,
        "M" => DobAccuracyEnum.Month,
        "E" => DobAccuracyEnum.Estimated,
        _ => DobAccuracyEnum.Day
    };

    public static AtsiStatusEnum MapAtsiStatus(bool isAboriginal, bool isTsi) => (isAboriginal, isTsi) switch
    {
        (true, true) => AtsiStatusEnum.Both,
        (true, false) => AtsiStatusEnum.AboriginalOnly,
        (false, true) => AtsiStatusEnum.TorresStraitIslanderOnly,
        _ => AtsiStatusEnum.NotAsked
    };

    /// <summary>
    /// Legacy's MaritalStatusId has no lookup table in the database (it's an application-level enum in
    /// the legacy C# source, not independently confirmed here) — this is a best-effort mapping based on
    /// common practice-management ordering, not a verified 1:1 translation.
    /// </summary>
    public static MaritalStatusEnum? MapMaritalStatus(int? code) => code switch
    {
        1 => MaritalStatusEnum.Single,
        2 => MaritalStatusEnum.Married,
        3 => MaritalStatusEnum.Divorced,
        4 => MaritalStatusEnum.Widowed,
        5 => MaritalStatusEnum.DeFacto,
        6 => MaritalStatusEnum.Separated,
        _ => null
    };
}
