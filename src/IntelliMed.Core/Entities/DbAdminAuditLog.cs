namespace IntelliMed.Core.Entities;

/// <summary>The only durable record of activity through the embedded DB admin tool (/dbadmin) —
/// it deliberately sits outside the normal Identity/RBAC system, so this is its whole audit trail.</summary>
public class DbAdminAuditLog
{
    public int Id { get; set; }
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;
    public DbAdminEventType EventType { get; set; }
    public string? Detail { get; set; }
    public string? IpAddress { get; set; }
}

public enum DbAdminEventType
{
    LoginSuccess,
    LoginFailure,
    QueryExecuted
}
