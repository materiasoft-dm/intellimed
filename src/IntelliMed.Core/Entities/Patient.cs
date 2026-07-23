namespace IntelliMed.Core.Entities;

public class Patient
{
    public int Id { get; set; }

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

    // Residential address (Postal/Other addresses live in PatientAddress)
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
    public string? HealthFundCode { get; set; }
    public string? HealthFundName { get; set; }
    public string? HealthFundNumber { get; set; }
    public string? HealthFundRef { get; set; }
    public string? HealthFundAliasFamily { get; set; }
    public string? HealthFundAliasFirst { get; set; }

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

    // eHealth (IHI)
    public string? IhiNumber { get; set; }
    public string? IhiRecordStatus { get; set; }
    public DateTime? IhiAssignedDate { get; set; }
    public string? IhiNumberStatus { get; set; }
    public DateTime? IhiUnresolvedDate { get; set; }

    // Lifecard
    public string? LifeCardNum { get; set; }

    public bool IsActive { get; set; } = true;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? UpdatedAt { get; set; }

    // Navigation properties
    public Patient? NextOfKinPatient { get; set; }
    public Patient? EmergencyContactPatient { get; set; }
    public Patient? PayerPatient { get; set; }
    public Practitioner? Provider { get; set; }
}
