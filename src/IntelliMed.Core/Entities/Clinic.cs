namespace IntelliMed.Core.Entities;

/// <summary>
/// A single practice location. Users are assigned to one or more clinics via <see cref="UserClinic"/>,
/// and clinical data (patients, appointments, invoices) is scoped to a clinic via ClinicId.
/// </summary>
public class Clinic
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
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
