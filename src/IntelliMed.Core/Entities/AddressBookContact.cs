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

    /// <summary>e.g. "GP", "Specialist", "Physiotherapist".</summary>
    public string? Profession { get; set; }
    public string? Speciality { get; set; }

    /// <summary>Medicare provider number — what a referral needs for a specialist claim.</summary>
    public string? ProviderNumber { get; set; }

    public string? Phone { get; set; }
    public string? Mobile { get; set; }
    public string? Fax { get; set; }
    public string? Email { get; set; }

    public string? Address { get; set; }
    public string? Suburb { get; set; }
    public string? Postcode { get; set; }
    public string? State { get; set; }

    /// <summary>The clinic or hospital this person works at, if known.</summary>
    public int? ClinicId { get; set; }
    public Clinic? Clinic { get; set; }

    public string? Notes { get; set; }

    public bool IsArchived { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? UpdatedAt { get; set; }

    /// <summary>The legacy Pracnet Contacts.GUID this person was imported from (PrimaryClinic Migrator) —
    /// the idempotency key for re-imports. Null for contacts created in IntelliMed.</summary>
    public string? LegacyGuid { get; set; }
}
