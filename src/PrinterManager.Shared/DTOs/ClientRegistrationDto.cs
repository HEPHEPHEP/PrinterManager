namespace PrinterManager.Shared.DTOs;

public class ClientRegistrationDto
{
    public required string Hostname { get; set; }
    public required string UserPrincipalName { get; set; }
    public string? IpAddress { get; set; }
    public string? OperatingSystem { get; set; }
    public List<InstalledPrinterDto> InstalledPrinters { get; set; } = new();
}

public class InstalledPrinterDto
{
    public required string PrinterName { get; set; }
    public string? PrinterPath { get; set; }
    public bool IsDefault { get; set; }
}
