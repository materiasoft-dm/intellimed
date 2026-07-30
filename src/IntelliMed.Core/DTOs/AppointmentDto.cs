using IntelliMed.Core.Entities;

namespace IntelliMed.Core.DTOs;

public class AppointmentDto
{
    public int Id { get; set; }
    public int ClientId { get; set; }
    public string ClientName { get; set; } = string.Empty;
    public int PractitionerId { get; set; }
    public string PractitionerName { get; set; } = string.Empty;
    public DateTime AppointmentDate { get; set; }
    public TimeSpan StartTime { get; set; }
    public TimeSpan EndTime { get; set; }
    public string StartTimeFormatted => DateTime.Today.Add(StartTime).ToString("h:mm tt");
    public string EndTimeFormatted => DateTime.Today.Add(EndTime).ToString("h:mm tt");
    public string TimeRangeFormatted => $"{StartTimeFormatted} - {EndTimeFormatted}";
    public AppointmentStatus Status { get; set; }
    public string StatusName => Status.ToString();
    public AppointmentType Type { get; set; }
    public string TypeName => Type.ToString();
    public string? Notes { get; set; }
    public bool IsBulkBill { get; set; }
    public decimal? Fee { get; set; }
    public DateTime? ArrivedAt { get; set; }
    public DateTime? SeenAt { get; set; }
    public DateTime? CompletedAt { get; set; }
    public Guid? RecurrenceSeriesId { get; set; }
    public int? AppointmentTypeSettingId { get; set; }
    public string? AppointmentTypeSettingName { get; set; }
    public string? AppointmentTypeSettingColorHex { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
}

public class CreateAppointmentDto
{
    public int ClinicId { get; set; }
    public int ClientId { get; set; }
    public int PractitionerId { get; set; }
    public DateTime AppointmentDate { get; set; }
    public TimeSpan StartTime { get; set; }
    public TimeSpan EndTime { get; set; }
    public AppointmentType Type { get; set; } = AppointmentType.Standard;
    public int? AppointmentTypeSettingId { get; set; }
    public string? Notes { get; set; }
    public bool IsBulkBill { get; set; }
    public decimal? Fee { get; set; }
}

/// <summary>Books a recurring series by materializing one Appointment row per occurrence, all sharing a new RecurrenceSeriesId.</summary>
public class CreateAppointmentSeriesDto : CreateAppointmentDto
{
    public RecurrenceFrequency Frequency { get; set; } = RecurrenceFrequency.Weekly;

    /// <summary>Exactly one of Occurrences or UntilDate must be set.</summary>
    public int? Occurrences { get; set; }
    public DateTime? UntilDate { get; set; }
}

public enum RecurrenceFrequency
{
    Weekly,
    Fortnightly,
    Monthly
}

public class UpdateAppointmentDto
{
    public int ClientId { get; set; }
    public int PractitionerId { get; set; }
    public DateTime AppointmentDate { get; set; }
    public TimeSpan StartTime { get; set; }
    public TimeSpan EndTime { get; set; }
    public AppointmentStatus Status { get; set; }
    public AppointmentType Type { get; set; }
    public int? AppointmentTypeSettingId { get; set; }
    public string? Notes { get; set; }
    public bool IsBulkBill { get; set; }
    public decimal? Fee { get; set; }
}

public class RescheduleAppointmentDto
{
    public DateTime AppointmentDate { get; set; }
    public TimeSpan StartTime { get; set; }
    public TimeSpan EndTime { get; set; }
}

public class AppointmentSearchDto
{
    public int? ClinicId { get; set; }
    public int? ClientId { get; set; }
    public int? PractitionerId { get; set; }
    public DateTime? FromDate { get; set; }
    public DateTime? ToDate { get; set; }
    public AppointmentStatus? Status { get; set; }
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 20;
}