namespace PrinterManager.Shared.Models;

public class Printer
{
    public int Id { get; set; }
    public required string PrinterId { get; set; }
    public required string Name { get; set; }
    public required string SharePath { get; set; }
    public string? Description { get; set; }
    public string? Location { get; set; }
    public string? ServerName { get; set; }
    public bool IsAvailable { get; set; } = true;
    public int? ReplacementPrinterId { get; set; }
    public Printer? ReplacementPrinter { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? LastModified { get; set; }

    public ICollection<PrinterAssignment> Assignments { get; set; } = new List<PrinterAssignment>();
}
