using Microsoft.Extensions.Logging;
using PrinterManager.Shared.DTOs;
using System.Management;
using System.Runtime.Versioning;

namespace PrinterManager.Client.Services;

public interface IPrinterDetectionService
{
    List<InstalledPrinterDto> GetInstalledPrinters();
    string? GetDefaultPrinter();
}

[SupportedOSPlatform("windows")]
public class PrinterDetectionService : IPrinterDetectionService
{
    private readonly ILogger<PrinterDetectionService> _logger;

    public PrinterDetectionService(ILogger<PrinterDetectionService> logger)
    {
        _logger = logger;
    }

    public List<InstalledPrinterDto> GetInstalledPrinters()
    {
        var printers = new List<InstalledPrinterDto>();

        try
        {
            var defaultPrinter = GetDefaultPrinter();

            // ManagementObjectSearcher/-Collection/-Object halten COM-Handles; ohne
            // Dispose leckt der Client bei jedem Poll Ressourcen.
            using var searcher = new ManagementObjectSearcher(
                "SELECT Name, Network, PortName, ShareName, ServerName FROM Win32_Printer");
            using var results = searcher.Get();

            foreach (ManagementObject printer in results)
            {
                using (printer)
                {
                    var name = printer["Name"]?.ToString();
                    if (string.IsNullOrEmpty(name))
                        continue;

                    var network = printer["Network"] as bool? ?? false;
                    var portName = printer["PortName"]?.ToString();

                    _logger.LogDebug(
                        "Drucker erkannt: Name={Name}, Netzwerk={Network}, Port={PortName}",
                        name, network, portName);

                    printers.Add(new InstalledPrinterDto
                    {
                        PrinterName = name,
                        PrinterPath = network ? ResolveNetworkPath(name, portName) : null,
                        IsDefault = string.Equals(name, defaultPrinter, StringComparison.OrdinalIgnoreCase)
                    });
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Fehler beim Erkennen der Drucker");
        }

        return printers;
    }

    public string? GetDefaultPrinter()
    {
        try
        {
            using var searcher = new ManagementObjectSearcher(
                "SELECT Name FROM Win32_Printer WHERE Default = True");
            using var results = searcher.Get();

            foreach (ManagementObject printer in results)
            {
                using (printer)
                {
                    var name = printer["Name"]?.ToString();
                    if (!string.IsNullOrEmpty(name))
                        return name;
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Fehler beim Ermitteln des Standarddruckers");
        }

        return null;
    }

    /// <summary>
    /// Ermittelt den UNC-Pfad eines Netzwerkdruckers. Windows benennt SMB-Verbindungen
    /// üblicherweise nach dem Pfad; andernfalls steht er im Portnamen.
    /// </summary>
    private static string ResolveNetworkPath(string name, string? portName)
    {
        if (name.StartsWith(@"\\", StringComparison.Ordinal))
            return name;

        if (!string.IsNullOrEmpty(portName) && portName.StartsWith(@"\\", StringComparison.Ordinal))
            return portName;

        return name;
    }
}
