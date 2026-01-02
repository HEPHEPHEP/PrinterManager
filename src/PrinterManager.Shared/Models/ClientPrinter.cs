namespace PrinterManager.Shared.Models;

public class ClientPrinter
{
    public int Id { get; set; }
    public int ClientId { get; set; }
    public Client? Client { get; set; }

    public string PrinterName { get; set; } = string.Empty;
    public string? PrinterPath { get; set; }
    public bool IsDefault { get; set; }
    public DateTime DetectedAt { get; set; } = DateTime.UtcNow;

    public int? ManagedPrinterId { get; set; }
}
