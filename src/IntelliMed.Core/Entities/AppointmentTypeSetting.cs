namespace IntelliMed.Core.Entities;

/// <summary>Clinic-configurable appointment-type catalogue (name + default duration + color), used to prefill the booking form.</summary>
public class AppointmentTypeSetting
{
    public int Id { get; set; }
    public int ClinicId { get; set; }
    public string Name { get; set; } = string.Empty;
    public int DefaultDurationMinutes { get; set; } = 15;
    public string ColorHex { get; set; } = "#4272d7";
    public bool IsActive { get; set; } = true;
    public int SortOrder { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
