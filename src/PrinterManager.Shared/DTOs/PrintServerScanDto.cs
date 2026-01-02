namespace PrinterManager.Shared.DTOs;

public class PrintServerScanDto
{
    public required string ServerName { get; set; }
    public string? Username { get; set; }
    public string? Password { get; set; }
}

public class ScannedPrinterDto
{
    public required string Name { get; set; }
    public required string ShareName { get; set; }
    public required string SharePath { get; set; }
    public string? Location { get; set; }
    public string? Comment { get; set; }
    public string? DriverName { get; set; }
}
