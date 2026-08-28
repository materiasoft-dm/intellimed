using IntelliMed.Core.Entities;

namespace IntelliMed.Core.DTOs;

public class EpisodeDto
{
    public int Id { get; set; }
    public int ClientId { get; set; }
    public string Title { get; set; } = string.Empty;
    public string? Description { get; set; }
    public EpisodeStatus Status { get; set; }
    public DateTime OpenedDate { get; set; }
    public DateTime? ClosedDate { get; set; }
    public bool IsArchived { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
}

public class CreateEpisodeDto
{
    public int ClientId { get; set; }
    public string Title { get; set; } = string.Empty;
    public string? Description { get; set; }
}

public class UpdateEpisodeDto
{
    public string Title { get; set; } = string.Empty;
    public string? Description { get; set; }
}
