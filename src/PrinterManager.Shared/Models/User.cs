namespace PrinterManager.Shared.Models;

public class User
{
    public int Id { get; set; }
    public required string UserPrincipalName { get; set; }
    public string? DisplayName { get; set; }
    public DateTime LastSeen { get; set; } = DateTime.UtcNow;
    public DateTime RegisteredAt { get; set; } = DateTime.UtcNow;
    public bool IsActive { get; set; } = true;

    public ICollection<PrinterAssignment> PrinterAssignments { get; set; } = new List<PrinterAssignment>();
}
