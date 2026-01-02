namespace PrinterManager.Shared.Models;

public class Client
{
    public int Id { get; set; }
    public required string Hostname { get; set; }
    public string? IpAddress { get; set; }
    public string? OperatingSystem { get; set; }
    public DateTime LastSeen { get; set; } = DateTime.UtcNow;
    public DateTime RegisteredAt { get; set; } = DateTime.UtcNow;
    public bool IsActive { get; set; } = true;

    public ICollection<ClientPrinter> InstalledPrinters { get; set; } = new List<ClientPrinter>();
    public ICollection<PrinterAssignment> PrinterAssignments { get; set; } = new List<PrinterAssignment>();
}
