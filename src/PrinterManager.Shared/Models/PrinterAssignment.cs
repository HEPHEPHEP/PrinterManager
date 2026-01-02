namespace PrinterManager.Shared.Models;

public enum AssignmentType
{
    User,
    Client
}

public class PrinterAssignment
{
    public int Id { get; set; }
    public int PrinterId { get; set; }
    public Printer? Printer { get; set; }

    public AssignmentType AssignmentType { get; set; }

    public int? UserId { get; set; }
    public User? User { get; set; }

    public int? ClientId { get; set; }
    public Client? Client { get; set; }

    public bool IsDefaultPrinter { get; set; }
    public DateTime AssignedAt { get; set; } = DateTime.UtcNow;
}
