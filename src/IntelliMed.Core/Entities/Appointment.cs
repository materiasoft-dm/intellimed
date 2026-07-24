namespace IntelliMed.Core.Entities;

public class Appointment
{
    public int Id { get; set; }
    public int ClinicId { get; set; }
    public int PatientId { get; set; }
    public int PractitionerId { get; set; }
    public DateTime AppointmentDate { get; set; }
    public TimeSpan StartTime { get; set; }
    public TimeSpan EndTime { get; set; }
    public AppointmentStatus Status { get; set; } = AppointmentStatus.Scheduled;
    public AppointmentType Type { get; set; } = AppointmentType.Standard;
    public string? Notes { get; set; }
    public bool IsBulkBill { get; set; }
    public decimal? Fee { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? UpdatedAt { get; set; }

    // Navigation properties
    public Patient? Patient { get; set; }
    public Practitioner? Practitioner { get; set; }
}

public enum AppointmentStatus
{
    Scheduled,
    Confirmed,
    InProgress,
    Completed,
    Cancelled,
    NoShow
}

public enum AppointmentType
{
    Standard,
    Long,
    Prolonged,
    Telehealth,
    HomeVisit
}