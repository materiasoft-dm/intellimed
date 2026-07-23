using IntelliMed.Core.Entities;

namespace IntelliMed.Core.DTOs;

public class PatientDto
{
    public int Id { get; set; }
    public PatientTypeEnum Type { get; set; }

    // Personal
    public string FirstName { get; set; } = string.Empty;
    public string LastName { get; set; } = string.Empty;
    public string FullName => $"{FirstName} {LastName}";
    public string? MiddleName { get; set; }
    public string? PreferredName { get; set; }
    public string? MaidenName { get; set; }
    public string? Title { get; set; }
    public GenderEnum? Gender { get; set; }
    public DateTime DateOfBirth { get; set; }
    public int Age => CalculateAge(DateOfBirth);
    public DobAccuracyEnum DobAccuracy { get; set; }
    public string? PlaceOfBirth { get; set; }
    public bool InterpreterRequired { get; set; }
    public string? InterpreterLanguage { get; set; }
    public MaritalStatusEnum? MaritalStatus { get; set; }
    public string? Ethnicity { get; set; }

    // Entitlement
    public string MedicareNumber { get; set; } = string.Empty;
    public string? MedicarePosition { get; set; }
    public DateTime? MedicareExpiryDate { get; set; }
    public string? DvaNumber { get; set; }
    public DateTime? DvaExpiryDate { get; set; }
    public string? PensionNumber { get; set; }
    public DateTime? PensionExpiryDate { get; set; }
    public string? EntitlementStatus { get; set; }
    public string? SafetyNetNumber { get; set; }
    public AtsiStatusEnum AtsiStatus { get; set; }
    public bool MedicareIncentiveEligible { get; set; }
    public bool CtgCoPaymentRelief { get; set; }

    // Residential address
    public string Address { get; set; } = string.Empty;
    public string Suburb { get; set; } = string.Empty;
    public string State { get; set; } = string.Empty;
    public string Postcode { get; set; } = string.Empty;
    public string FullAddress => string.IsNullOrWhiteSpace(Address)
        ? string.Empty
        : $"{Address}, {Suburb} {State} {Postcode}";

    // Contact Details
    public string Email { get; set; } = string.Empty;
    public string Phone { get; set; } = string.Empty;
    public string? BusinessHoursPhone { get; set; }
    public string? MobilePhone { get; set; }
    public string? FaxNumber { get; set; }
    public bool AcceptSms { get; set; }
    public bool AcceptEmail { get; set; }
    public bool AcceptOnlineAppointments { get; set; }
    public bool AcceptSmsMarketing { get; set; }
    public string? Notes { get; set; }
    public string? Warnings { get; set; }
    public int? NextOfKinPatientId { get; set; }
    public string? NextOfKinName { get; set; }
    public string? NextOfKinPhone { get; set; }
    public int? EmergencyContactPatientId { get; set; }
    public string? EmergencyContactName { get; set; }
    public string? EmergencyContactPhone { get; set; }
    public bool SameAsNextOfKin { get; set; }

    // Health Fund
    public int? HealthFundId { get; set; }
    public string? HealthFundCode { get; set; }
    public string? HealthFundName { get; set; }
    public string? HealthFundNumber { get; set; }
    public string? HealthFundRef { get; set; }
    public string? HealthFundAliasFamily { get; set; }
    public string? HealthFundAliasFirst { get; set; }
    public DateTime? HealthFundJoinDate { get; set; }

    // Account
    public AccountTypeEnum AccountType { get; set; }
    public string? FeeRateCode { get; set; }
    public int? PayerPatientId { get; set; }
    public string? PayerName { get; set; }
    public string? AccountName { get; set; }
    public string? AccountBsb { get; set; }
    public string? AccountNumber { get; set; }
    public bool UseMedicareRegisteredBankAccount { get; set; }

    // File
    public string? FileNumber { get; set; }
    public string? UrNumber { get; set; }
    public bool Deceased { get; set; }
    public int? ProviderId { get; set; }
    public DateTime? LastSeenDate { get; set; }

    // eHealth
    public string? IhiNumber { get; set; }
    public string? IhiRecordStatus { get; set; }
    public DateTime? IhiAssignedDate { get; set; }
    public string? IhiNumberStatus { get; set; }
    public DateTime? IhiUnresolvedDate { get; set; }

    // Lifecard
    public string? LifeCardNum { get; set; }

    public bool IsActive { get; set; } = true;
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }

    private static int CalculateAge(DateTime dateOfBirth)
    {
        var today = DateTime.Today;
        var age = today.Year - dateOfBirth.Year;
        if (dateOfBirth.Date > today.AddYears(-age)) age--;
        return age;
    }
}

public class CreatePatientDto
{
    public PatientTypeEnum Type { get; set; } = PatientTypeEnum.Person;

    // Personal
    public string FirstName { get; set; } = string.Empty;
    public string LastName { get; set; } = string.Empty;
    public string? MiddleName { get; set; }
    public string? PreferredName { get; set; }
    public string? MaidenName { get; set; }
    public string? Title { get; set; }
    public GenderEnum? Gender { get; set; }
    public DateTime DateOfBirth { get; set; }
    public DobAccuracyEnum DobAccuracy { get; set; } = DobAccuracyEnum.Day;
    public string? PlaceOfBirth { get; set; }
    public bool InterpreterRequired { get; set; }
    public string? InterpreterLanguage { get; set; }
    public MaritalStatusEnum? MaritalStatus { get; set; }
    public string? Ethnicity { get; set; }

    // Entitlement
    public string MedicareNumber { get; set; } = string.Empty;
    public string? MedicarePosition { get; set; }
    public DateTime? MedicareExpiryDate { get; set; }
    public string? DvaNumber { get; set; }
    public DateTime? DvaExpiryDate { get; set; }
    public string? PensionNumber { get; set; }
    public DateTime? PensionExpiryDate { get; set; }
    public string? EntitlementStatus { get; set; }
    public string? SafetyNetNumber { get; set; }
    public AtsiStatusEnum AtsiStatus { get; set; } = AtsiStatusEnum.NotAsked;
    public bool MedicareIncentiveEligible { get; set; }
    public bool CtgCoPaymentRelief { get; set; }

    // Residential address
    public string Address { get; set; } = string.Empty;
    public string Suburb { get; set; } = string.Empty;
    public string State { get; set; } = string.Empty;
    public string Postcode { get; set; } = string.Empty;

    // Contact Details
    public string Email { get; set; } = string.Empty;
    public string Phone { get; set; } = string.Empty;
    public string? BusinessHoursPhone { get; set; }
    public string? MobilePhone { get; set; }
    public string? FaxNumber { get; set; }
    public bool AcceptSms { get; set; }
    public bool AcceptEmail { get; set; }
    public bool AcceptOnlineAppointments { get; set; }
    public bool AcceptSmsMarketing { get; set; }
    public string? Notes { get; set; }
    public string? Warnings { get; set; }
    public int? NextOfKinPatientId { get; set; }
    public string? NextOfKinName { get; set; }
    public string? NextOfKinPhone { get; set; }
    public int? EmergencyContactPatientId { get; set; }
    public string? EmergencyContactName { get; set; }
    public string? EmergencyContactPhone { get; set; }
    public bool SameAsNextOfKin { get; set; }

    // Health Fund
    public int? HealthFundId { get; set; }
    public string? HealthFundNumber { get; set; }
    public string? HealthFundRef { get; set; }
    public string? HealthFundAliasFamily { get; set; }
    public string? HealthFundAliasFirst { get; set; }
    public DateTime? HealthFundJoinDate { get; set; }

    // Account
    public AccountTypeEnum AccountType { get; set; } = AccountTypeEnum.PrivatePatient;
    public string? FeeRateCode { get; set; }
    public int? PayerPatientId { get; set; }
    public string? PayerName { get; set; }
    public string? AccountName { get; set; }
    public string? AccountBsb { get; set; }
    public string? AccountNumber { get; set; }
    public bool UseMedicareRegisteredBankAccount { get; set; }

    // File
    public string? FileNumber { get; set; }
    public string? UrNumber { get; set; }
    public bool Deceased { get; set; }
    public int? ProviderId { get; set; }
    public DateTime? LastSeenDate { get; set; }

    // eHealth
    public string? IhiNumber { get; set; }
    public string? IhiRecordStatus { get; set; }
    public DateTime? IhiAssignedDate { get; set; }
    public string? IhiNumberStatus { get; set; }
    public DateTime? IhiUnresolvedDate { get; set; }

    // Lifecard
    public string? LifeCardNum { get; set; }
}

public class UpdatePatientDto
{
    public PatientTypeEnum Type { get; set; } = PatientTypeEnum.Person;

    // Personal
    public string FirstName { get; set; } = string.Empty;
    public string LastName { get; set; } = string.Empty;
    public string? MiddleName { get; set; }
    public string? PreferredName { get; set; }
    public string? MaidenName { get; set; }
    public string? Title { get; set; }
    public GenderEnum? Gender { get; set; }
    public DateTime DateOfBirth { get; set; }
    public DobAccuracyEnum DobAccuracy { get; set; }
    public string? PlaceOfBirth { get; set; }
    public bool InterpreterRequired { get; set; }
    public string? InterpreterLanguage { get; set; }
    public MaritalStatusEnum? MaritalStatus { get; set; }
    public string? Ethnicity { get; set; }

    // Entitlement
    public string MedicareNumber { get; set; } = string.Empty;
    public string? MedicarePosition { get; set; }
    public DateTime? MedicareExpiryDate { get; set; }
    public string? DvaNumber { get; set; }
    public DateTime? DvaExpiryDate { get; set; }
    public string? PensionNumber { get; set; }
    public DateTime? PensionExpiryDate { get; set; }
    public string? EntitlementStatus { get; set; }
    public string? SafetyNetNumber { get; set; }
    public AtsiStatusEnum AtsiStatus { get; set; }
    public bool MedicareIncentiveEligible { get; set; }
    public bool CtgCoPaymentRelief { get; set; }

    // Residential address
    public string Address { get; set; } = string.Empty;
    public string Suburb { get; set; } = string.Empty;
    public string State { get; set; } = string.Empty;
    public string Postcode { get; set; } = string.Empty;

    // Contact Details
    public string Email { get; set; } = string.Empty;
    public string Phone { get; set; } = string.Empty;
    public string? BusinessHoursPhone { get; set; }
    public string? MobilePhone { get; set; }
    public string? FaxNumber { get; set; }
    public bool AcceptSms { get; set; }
    public bool AcceptEmail { get; set; }
    public bool AcceptOnlineAppointments { get; set; }
    public bool AcceptSmsMarketing { get; set; }
    public string? Notes { get; set; }
    public string? Warnings { get; set; }
    public int? NextOfKinPatientId { get; set; }
    public string? NextOfKinName { get; set; }
    public string? NextOfKinPhone { get; set; }
    public int? EmergencyContactPatientId { get; set; }
    public string? EmergencyContactName { get; set; }
    public string? EmergencyContactPhone { get; set; }
    public bool SameAsNextOfKin { get; set; }

    // Health Fund
    public int? HealthFundId { get; set; }
    public string? HealthFundCode { get; set; }
    public string? HealthFundName { get; set; }
    public string? HealthFundNumber { get; set; }
    public string? HealthFundRef { get; set; }
    public string? HealthFundAliasFamily { get; set; }
    public string? HealthFundAliasFirst { get; set; }
    public DateTime? HealthFundJoinDate { get; set; }

    // Account
    public AccountTypeEnum AccountType { get; set; }
    public string? FeeRateCode { get; set; }
    public int? PayerPatientId { get; set; }
    public string? PayerName { get; set; }
    public string? AccountName { get; set; }
    public string? AccountBsb { get; set; }
    public string? AccountNumber { get; set; }
    public bool UseMedicareRegisteredBankAccount { get; set; }

    // File
    public string? FileNumber { get; set; }
    public string? UrNumber { get; set; }
    public bool Deceased { get; set; }
    public int? ProviderId { get; set; }
    public DateTime? LastSeenDate { get; set; }

    // eHealth
    public string? IhiNumber { get; set; }
    public string? IhiRecordStatus { get; set; }
    public DateTime? IhiAssignedDate { get; set; }
    public string? IhiNumberStatus { get; set; }
    public DateTime? IhiUnresolvedDate { get; set; }

    // Lifecard
    public string? LifeCardNum { get; set; }

    public bool IsActive { get; set; } = true;
}

public class PatientSearchDto
{
    public string? Query { get; set; }
    public bool? IsActive { get; set; }
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 20;

    // Basic
    public string? Surname { get; set; }
    public string? GivenName { get; set; }
    public string? MedicareNumber { get; set; }
    public GenderEnum? Gender { get; set; }
    public string? DvaNumber { get; set; }
    public string? FileNumber { get; set; }
    public string? PensionNumber { get; set; }
    public string? HealthFundNumber { get; set; }
    public string? LifeCardNum { get; set; }
    public DateTime? DobFrom { get; set; }
    public DateTime? DobTo { get; set; }

    // Residential address
    public string? Address { get; set; }
    public string? Suburb { get; set; }
    public string? Postcode { get; set; }
    public string? State { get; set; }

    // Postal address (matched against PatientAddress rows of type Postal)
    public string? PostalAddress { get; set; }
    public string? PostalSuburb { get; set; }
    public string? PostalPostcode { get; set; }
    public string? PostalState { get; set; }

    // Contact
    public string? HomePhone { get; set; }
    public string? BusinessHoursPhone { get; set; }
    public string? MobilePhone { get; set; }
    public string? Email { get; set; }
    public AtsiStatusEnum? AtsiStatus { get; set; }

    // Date ranges
    public DateTime? CreatedFrom { get; set; }
    public DateTime? CreatedTo { get; set; }
    public DateTime? MedicareExpiryFrom { get; set; }
    public DateTime? MedicareExpiryTo { get; set; }
    public DateTime? HealthFundJoinFrom { get; set; }
    public DateTime? HealthFundJoinTo { get; set; }

    // Misc / account
    public string? Warnings { get; set; }
    public string? Notes { get; set; }
    public string? ReferredBy { get; set; }
    public PatientTypeEnum? PatientType { get; set; }
    public string? UrNumber { get; set; }
    public int? HealthFundId { get; set; }
    public int? PayerPatientId { get; set; }
    public List<AccountTypeEnum>? AccountTypes { get; set; }

    // Flags
    public bool? Deceased { get; set; }
    public bool IncludeArchived { get; set; }
    public bool? AcceptEmail { get; set; }
    public bool? AcceptSms { get; set; }
    public bool? AcceptSmsMarketing { get; set; }
}
