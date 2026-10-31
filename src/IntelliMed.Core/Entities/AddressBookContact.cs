namespace IntelliMed.Core.Entities;

/// <summary>
/// A person in the practice's address book — a GP, specialist or allied health provider the clinic
/// refers to or receives referrals from. Organisations are not stored here: an organisation is a
/// <see cref="Client"/> with <see cref="ClientTypeEnum.Organisation"/>, and a contact's workplace is a
/// <see cref="Clinic"/> (clinic or hospital). Shared across all of the practice's clinics.
/// </summary>
public class AddressBookContact
{
    public int Id { get; set; }

    public string? Title { get; set; }
    public string? FirstName { get; set; }
    public string LastName { get; set; } = string.Empty;
    /// <summary>The name they go by, if different (Medinet's "Nickname").</summary>
    public string? PreferredName { get; set; }
    public GenderEnum? Gender { get; set; }

    /// <summary>e.g. "GP", "Specialist", "Physiotherapist".</summary>
    public string? Profession { get; set; }
    public string? Speciality { get; set; }

    /// <summary>Medicare provider number — what a referral needs for a specialist claim.</summary>
    public string? ProviderNumber { get; set; }

    /// <summary>Healthcare Provider Identifier – Individual (16 digits), from the HI Service.</summary>
    public string? Hpii { get; set; }
    /// <summary>The HI Service provider type code that goes with the HPI-I.</summary>
    public string? ProviderTypeCode { get; set; }

    /// <summary>Australian Business / Company Number of their practice.</summary>
    public string? Abn { get; set; }
    public string? Acn { get; set; }

    public string? Phone { get; set; }
    public string? Mobile { get; set; }
    public string? Fax { get; set; }
    public string? Email { get; set; }

    public string? Address { get; set; }
    public string? AddressLine2 { get; set; }
    public string? Suburb { get; set; }
    public string? Postcode { get; set; }
    public string? State { get; set; }

    /// <summary>The clinic or hospital this person works at, if known.</summary>
    public int? ClinicId { get; set; }
    public Clinic? Clinic { get; set; }

    /// <summary>Who correspondence should be marked for the attention of (e.g. a practice manager).</summary>
    public string? Attention { get; set; }

    /// <summary>Secure-messaging address (e.g. a ReferralNet / HealthLink id) and the network it belongs to.</summary>
    public string? MessagingId { get; set; }
    public string? MessagingNetwork { get; set; }
    /// <summary>They accept reports and letters by secure messaging.</summary>
    public bool AcceptsSecureMessaging { get; set; }

    /// <summary>Their id in an external directory or system (Medinet's "External Id").</summary>
    public string? ExternalId { get; set; }

    public string? Notes { get; set; }

    public bool IsArchived { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? UpdatedAt { get; set; }

    /// <summary>The legacy Pracnet Contacts.GUID this person was imported from (PrimaryClinic Migrator) —
    /// the idempotency key for re-imports. Null for contacts created in IntelliMed.</summary>
    public string? LegacyGuid { get; set; }
}
