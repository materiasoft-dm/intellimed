namespace IntelliMed.Core.Entities;

/// <summary>
/// Join entity assigning an ApplicationUser to a Clinic (many-to-many).
/// </summary>
public class UserClinic
{
    public int Id { get; set; }
    public string ApplicationUserId { get; set; } = string.Empty;
    public int ClinicId { get; set; }

    public ApplicationUser? ApplicationUser { get; set; }
    public Clinic? Clinic { get; set; }
}
