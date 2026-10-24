using System.ComponentModel.DataAnnotations;
using IntelliMed.Core.Entities;

namespace IntelliMed.Core.DTOs;

public class ClientDto
{
    public int Id { get; set; }
    public int ClinicId { get; set; }
    public ClientTypeEnum Type { get; set; }

    // Personal
    public string FirstName { get; set; } = string.Empty;
    public string LastName { get; set; } = string.Empty;
    public string FullName => $"{FirstName} {LastName}";
    public string? MiddleName { get; set; }
    public string? PreferredName { get; set; }
    public string? MaidenName { get; set; }
    public string? Title { get; set; }
    public GenderEnum? Gender { get; set; }
    public DateTime? DateOfBirth { get; set; }
    /// <summary>Null for an organisation, which has no date of birth.</summary>
    public int? Age => DateOfBirth.HasValue ? CalculateAge(DateOfBirth.Value) : null;
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
    public int? NextOfKinClientId { get; set; }
    public string? NextOfKinName { get; set; }
    public string? NextOfKinPhone { get; set; }
    public int? EmergencyContactClientId { get; set; }
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
    public int? PayerClientId { get; set; }
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

    // Provider visibility
    public bool VisibleToAllProviders { get; set; }
    public List<ClientProviderAccessDto> ProviderAccess { get; set; } = new();

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

public class CreateClientDto : IValidatableObject
{
    /// <summary>Set server-side from the caller's current clinic context (X-Clinic-Id header), not client-supplied.</summary>
    public int ClinicId { get; set; }

    public ClientTypeEnum Type { get; set; } = ClientTypeEnum.Person;

    // Personal
    public string FirstName { get; set; } = string.Empty;
    public string LastName { get; set; } = string.Empty;
    public string? MiddleName { get; set; }
    public string? PreferredName { get; set; }
    public string? MaidenName { get; set; }
    public string? Title { get; set; }
    public GenderEnum? Gender { get; set; }
    public DateTime? DateOfBirth { get; set; }

    /// <summary>A person must have a date of birth; an organisation has none.</summary>
    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (Type == ClientTypeEnum.Person && DateOfBirth == null)
            yield return new ValidationResult("Date of birth is required.", new[] { nameof(DateOfBirth) });
    }

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
    public int? NextOfKinClientId { get; set; }
    public string? NextOfKinName { get; set; }
    public string? NextOfKinPhone { get; set; }
    public int? EmergencyContactClientId { get; set; }
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
    public int? PayerClientId { get; set; }
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

    // Provider visibility
    /// <summary>Set server-side from the caller's linked Practitioner (never client-supplied) — the
    /// creator, auto-granted access and locked in permanently. Null for non-clinician creators
    /// (Receptionist/Admin), who instead force VisibleToAllProviders true regardless of what's posted.</summary>
    public int? CreatedByPractitionerId { get; set; }
    /// <summary>Defaults to false (bool's natural default) — a linked Doctor/Nurse creating their
    /// own patient defaults to visible-only-to-them unless they explicitly opt in. The controller
    /// forces this true regardless of what's posted when the creator has no linked Practitioner.</summary>
    public bool VisibleToAllProviders { get; set; }
    /// <summary>Additional practitioners to grant access to, beyond the auto-added creator.</summary>
    public List<int> ProviderIds { get; set; } = new();

    // eHealth
    public string? IhiNumber { get; set; }
    public string? IhiRecordStatus { get; set; }
    public DateTime? IhiAssignedDate { get; set; }
    public string? IhiNumberStatus { get; set; }
    public DateTime? IhiUnresolvedDate { get; set; }

    // Lifecard
    public string? LifeCardNum { get; set; }
}

public class UpdateClientDto : IValidatableObject
{
    public ClientTypeEnum Type { get; set; } = ClientTypeEnum.Person;

    // Personal
    public string FirstName { get; set; } = string.Empty;
    public string LastName { get; set; } = string.Empty;
    public string? MiddleName { get; set; }
    public string? PreferredName { get; set; }
    public string? MaidenName { get; set; }
    public string? Title { get; set; }
    public GenderEnum? Gender { get; set; }
    public DateTime? DateOfBirth { get; set; }

    /// <summary>A person must have a date of birth; an organisation has none.</summary>
    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (Type == ClientTypeEnum.Person && DateOfBirth == null)
            yield return new ValidationResult("Date of birth is required.", new[] { nameof(DateOfBirth) });
    }

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
    public int? NextOfKinClientId { get; set; }
    public string? NextOfKinName { get; set; }
    public string? NextOfKinPhone { get; set; }
    public int? EmergencyContactClientId { get; set; }
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
    public AccountTypeEnum AccountType { get; set; }
    public string? FeeRateCode { get; set; }
    public int? PayerClientId { get; set; }
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

    // Provider visibility
    public bool VisibleToAllProviders { get; set; }
    /// <summary>Additional practitioners to grant access to, beyond the (immutable) creator.</summary>
    public List<int> ProviderIds { get; set; } = new();

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

public class ClientSearchDto
{
    public string? Query { get; set; }
    public bool? IsActive { get; set; }
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 20;

    /// <summary>Set server-side from the caller's current clinic context (X-Clinic-Id header). Null = no clinic filtering.</summary>
    public int? ClinicId { get; set; }

    /// <summary>Set server-side from the caller's linked Practitioner (never client-supplied). Null
    /// means unrestricted (SuperAdmin/Admin, or a login with no linked Practitioner) — a value
    /// limits results to clients visible to that practitioner. See ClientRepository.BuildSearchQuery.</summary>
    public int? RestrictToPractitionerId { get; set; }

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

    // Postal address (matched against ClientAddress rows of type Postal)
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
    public ClientTypeEnum? ClientType { get; set; }
    public string? UrNumber { get; set; }
    public int? HealthFundId { get; set; }
    public int? PayerClientId { get; set; }
    public List<AccountTypeEnum>? AccountTypes { get; set; }

    // Flags
    public bool? Deceased { get; set; }
    public bool IncludeArchived { get; set; }
    public bool? AcceptEmail { get; set; }
    public bool? AcceptSms { get; set; }
    public bool? AcceptSmsMarketing { get; set; }
}
