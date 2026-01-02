namespace PrinterManager.Shared.DTOs;

public class PrinterDto
{
    public int Id { get; set; }
    public required string PrinterId { get; set; }
    public required string Name { get; set; }
    public required string SharePath { get; set; }
    public string? Description { get; set; }
    public string? Location { get; set; }
    public string? ServerName { get; set; }
    public bool IsAvailable { get; set; }
    public int? ReplacementPrinterId { get; set; }
    public string? ReplacementPrinterName { get; set; }
}

public class CreatePrinterDto
{
    public required string PrinterId { get; set; }
    public required string Name { get; set; }
    public required string SharePath { get; set; }
    public string? Description { get; set; }
    public string? Location { get; set; }
    public string? ServerName { get; set; }
}

public class UpdatePrinterDto
{
    public string? Name { get; set; }
    public string? Description { get; set; }
    public string? Location { get; set; }
    public bool? IsAvailable { get; set; }
    public int? ReplacementPrinterId { get; set; }
}
