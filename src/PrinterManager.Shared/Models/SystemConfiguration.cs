namespace PrinterManager.Shared.Models;

public enum AssignmentPriority
{
    UserPriority,
    ClientPriority
}

public class SystemConfiguration
{
    public int Id { get; set; }
    public AssignmentPriority AssignmentPriority { get; set; } = AssignmentPriority.UserPriority;
    public bool AutoAssignReplacementPrinters { get; set; } = true;
    public DateTime LastModified { get; set; } = DateTime.UtcNow;
}
