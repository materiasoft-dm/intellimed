namespace IntelliMed.Core.Entities;

/// <summary>
/// A single practice location — a clinic or a hospital. Users are assigned to one or more clinics via
/// <see cref="UserClinic"/>, and clinical data (patients, appointments, invoices) is scoped to a clinic
/// via ClinicId. Also serves as an address-book contact's workplace (a specialist's clinic or
/// hospital); those never have users assigned, so they never appear in anyone's clinic switcher.
/// </summary>
public class Clinic
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public ClinicType Type { get; set; } = ClinicType.Clinic;
    public string? Abn { get; set; }
    public string? Phone { get; set; }
    public string? Fax { get; set; }
    public string? Email { get; set; }
    public string? Address { get; set; }
    public string? Suburb { get; set; }
    public string? Postcode { get; set; }
    public string? State { get; set; }
    public bool IsActive { get; set; } = true;

    public ICollection<UserClinic> UserClinics { get; set; } = new List<UserClinic>();
}

/// <summary>Persisted as an int — append new values, never reorder.</summary>
public enum ClinicType
{
    Clinic,
    Hospital
}
