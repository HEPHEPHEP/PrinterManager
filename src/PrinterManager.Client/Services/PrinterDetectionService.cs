using System.Management;
using PrinterManager.Shared.DTOs;
using Microsoft.Extensions.Logging;

namespace PrinterManager.Client.Services;

public interface IPrinterDetectionService
{
    List<InstalledPrinterDto> GetInstalledPrinters();
    string? GetDefaultPrinter();
}

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
            var searcher = new ManagementObjectSearcher("SELECT * FROM Win32_Printer");
            var defaultPrinter = GetDefaultPrinter();

            foreach (ManagementObject printer in searcher.Get())
            {
                var name = printer["Name"]?.ToString();
                var network = printer["Network"]?.ToString();
                var portName = printer["PortName"]?.ToString();
                var shareName = printer["ShareName"]?.ToString();
                var serverName = printer["ServerName"]?.ToString();

                _logger.LogDebug($"Detected printer: Name='{name}', Network={network}, PortName='{portName}', ShareName='{shareName}', ServerName='{serverName}'");

                if (!string.IsNullOrEmpty(name))
                {
                    string? printerPath = null;

                    // For network printers, try to get the actual share path
                    if (network == "True")
                    {
                        _logger.LogDebug($"Processing network printer: {name}");

                        // If the printer name starts with \\, it's already the share path
                        if (name.StartsWith(@"\\"))
                        {
                            printerPath = name;
                            _logger.LogDebug($"  -> Using name as path: {printerPath}");
                        }
                        // Otherwise, check if the port name contains the share path
                        else if (!string.IsNullOrEmpty(portName) && portName.StartsWith(@"\\"))
                        {
                            printerPath = portName;
                            _logger.LogDebug($"  -> Using port name as path: {printerPath}");
                        }
                        // Last resort: use the printer name
                        else
                        {
                            printerPath = name;
                            _logger.LogDebug($"  -> Using name as fallback path: {printerPath}");
                        }
                    }
                    else
                    {
                        _logger.LogDebug($"Skipping local printer: {name}");
                    }

                    printers.Add(new InstalledPrinterDto
                    {
                        PrinterName = name,
                        PrinterPath = printerPath,
                        IsDefault = name == defaultPrinter
                    });
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error detecting printers");
        }

        return printers;
    }

    public string? GetDefaultPrinter()
    {
        try
        {
            var searcher = new ManagementObjectSearcher("SELECT * FROM Win32_Printer WHERE Default = True");
            foreach (ManagementObject printer in searcher.Get())
            {
                return printer["Name"]?.ToString();
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting default printer");
        }

        return null;
    }
}
