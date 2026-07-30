namespace IntelliMed.Core.DTOs;

public class AppointmentTypeSettingDto
{
    public int Id { get; set; }
    public int ClinicId { get; set; }
    public string Name { get; set; } = string.Empty;
    public int DefaultDurationMinutes { get; set; }
    public string ColorHex { get; set; } = string.Empty;
    public bool IsActive { get; set; }
    public int SortOrder { get; set; }
}

public class CreateAppointmentTypeSettingDto
{
    public int ClinicId { get; set; }
    public string Name { get; set; } = string.Empty;
    public int DefaultDurationMinutes { get; set; } = 15;
    public string ColorHex { get; set; } = "#4272d7";
    public int SortOrder { get; set; }
}

public class UpdateAppointmentTypeSettingDto
{
    public string Name { get; set; } = string.Empty;
    public int DefaultDurationMinutes { get; set; }
    public string ColorHex { get; set; } = string.Empty;
    public bool IsActive { get; set; }
    public int SortOrder { get; set; }
}
