namespace IntelliMed.Core.Entities;

public class Client
{
    public int Id { get; set; }

    public int ClinicId { get; set; }
    public Clinic? Clinic { get; set; }

    public ClientTypeEnum Type { get; set; } = ClientTypeEnum.Person;

    // Personal
    public string FirstName { get; set; } = string.Empty;
    public string LastName { get; set; } = string.Empty;
    public string? MiddleName { get; set; }
    public string? PreferredName { get; set; }
    public string? MaidenName { get; set; }
    public string? Title { get; set; }
    public GenderEnum? Gender { get; set; }
    /// <summary>Null for an organisation; required for a person (enforced by Create/UpdateClientDto).</summary>
    public DateTime? DateOfBirth { get; set; }
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

    // Residential address (Postal/Other addresses live in ClientAddress)
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

    // eHealth (IHI)
    public string? IhiNumber { get; set; }
    public string? IhiRecordStatus { get; set; }
    public DateTime? IhiAssignedDate { get; set; }
    public string? IhiNumberStatus { get; set; }
    public DateTime? IhiUnresolvedDate { get; set; }

    // Lifecard
    public string? LifeCardNum { get; set; }

    // Brought across from Medinet's patient record (TPatients)
    /// <summary>DVA card colour: GOLD, WHITE or ORANGE — decides what DVA covers.</summary>
    public string? DvaCardType { get; set; }
    /// <summary>Concession card held, e.g. CPC (Pensioner Concession), HCC (Health Care), CCSH (Commonwealth Seniors).</summary>
    public string? ConcessionCardType { get; set; }
    /// <summary>Health care card date (Medinet DATE_HCC).</summary>
    public DateTime? HealthCareCardDate { get; set; }
    /// <summary>Medicare safety net card type (e.g. MSAFENET).</summary>
    public string? SafetyNetCardType { get; set; }
    public DateTime? SafetyNetExpiryDate { get; set; }
    /// <summary>PBS safety net / entitlement card number.</summary>
    public string? PbsEntitlementNumber { get; set; }
    /// <summary>PBS entitlement card colour/type.</summary>
    public string? PbsEntitlementCardType { get; set; }
    public DateTime? PbsEntitlementExpiryDate { get; set; }
    public string? AtsiAccreditation { get; set; }
    /// <summary>Level of health fund cover.</summary>
    public string? HealthFundCoverLevel { get; set; }
    public string? CountryOfBirth { get; set; }
    public string? CulturalBackground { get; set; }
    /// <summary>Language they prefer to use — separate from the interpreter language.</summary>
    public string? PreferredLanguage { get; set; }
    /// <summary>How letters open ("Dear ...").</summary>
    public string? Salutation { get; set; }
    public DateTime? DateOfDeath { get; set; }
    public string? NextOfKinRelationship { get; set; }
    public string? EmergencyContactRelationship { get; set; }
    /// <summary>e.g. "Private Property".</summary>
    public string? ResidentialAddressType { get; set; }
    /// <summary>Post correspondence to the postal address rather than the residential one.</summary>
    public bool SendToPostalAddress { get; set; }
    /// <summary>How they heard about the practice.</summary>
    public string? HowDidYouHear { get; set; }
    /// <summary>Bill this patient on one invoice per visit rather than combining with family.</summary>
    public bool OneInvoicePerPatient { get; set; }
    public bool PaysByEft { get; set; }
    /// <summary>Account statements are held (not sent) until this date.</summary>
    public DateTime? StatementHoldUntil { get; set; }
    /// <summary>Sub-number under the file number (Medinet MinorFileNum).</summary>
    public string? MinorFileNumber { get; set; }
    /// <summary>Id in an external system.</summary>
    public string? ExternalId { get; set; }
    public string? PatientPortalId { get; set; }
    /// <summary>Consented to their de-identified data being used for research.</summary>
    public bool ResearchConsent { get; set; }
    public string? PreferredPharmacyCode1 { get; set; }
    public string? PreferredPharmacyCode2 { get; set; }
    /// <summary>Do not send this patient electronic prescriptions.</summary>
    public bool DisableEPrescribing { get; set; }
    public bool ShowEPrescriptionMessage { get; set; }

    public bool IsActive { get; set; } = true;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? UpdatedAt { get; set; }

    /// <summary>The legacy Pracnet Contacts.GUID this record was imported from (PrimaryClinic Migrator) — the idempotency key for re-imports. Null for clients created natively in IntelliMed.</summary>
    public string? LegacyGuid { get; set; }

    // Provider visibility — see ClientProviderAccess for the explicit per-practitioner grant list.
    // When true, every staff member can see this client and the grant list below isn't consulted.
    public bool VisibleToAllProviders { get; set; } = true;

    /// <summary>The practitioner who created this record, if the creator was a linked Doctor/Nurse
    /// (see Practitioner.ApplicationUserId). Always implicitly granted access and can never be
    /// removed from the provider-access list, regardless of who edits it later. Null if this client
    /// was created by a non-clinician (Receptionist/Admin) — those default VisibleToAllProviders=true.</summary>
    public int? CreatedByPractitionerId { get; set; }
    public Practitioner? CreatedByPractitioner { get; set; }

    // Navigation properties
    public Client? NextOfKinClient { get; set; }
    public Client? EmergencyContactClient { get; set; }
    public Client? PayerClient { get; set; }
    public Practitioner? Provider { get; set; }
    public HealthFund? HealthFund { get; set; }
    public ICollection<ClientProviderAccess> ProviderAccess { get; set; } = new List<ClientProviderAccess>();
}
