namespace IntelliMed.Core.Entities;

public class Appointment
{
    public int Id { get; set; }
    public int ClinicId { get; set; }
    public int ClientId { get; set; }
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

    // Waiting-room / visit-lifecycle timestamps
    public DateTime? ArrivedAt { get; set; }
    public DateTime? SeenAt { get; set; }
    public DateTime? CompletedAt { get; set; }

    // Recurring series — occurrences are materialized up front and share this id
    public Guid? RecurrenceSeriesId { get; set; }

    // Optional link to a clinic-configurable duration preset
    public int? AppointmentTypeSettingId { get; set; }

    // Navigation properties
    public Client? Client { get; set; }
    public Practitioner? Practitioner { get; set; }
    public AppointmentTypeSetting? AppointmentTypeSetting { get; set; }
}

public enum AppointmentStatus
{
    Scheduled,
    Confirmed,
    InProgress,
    Completed,
    Cancelled,
    NoShow,
    // Appended at the end — EF stores this enum as int, so inserting a value earlier
    // in the sequence would silently reinterpret every existing row's stored status.
    Arrived
}

public enum AppointmentType
{
    Standard,
    Long,
    Prolonged,
    Telehealth,
    HomeVisit
}