using System.Management;
using PrinterManager.Shared.DTOs;

namespace PrinterManager.Client.Services;

public interface IPrinterDetectionService
{
    List<InstalledPrinterDto> GetInstalledPrinters();
    string? GetDefaultPrinter();
}

public class PrinterDetectionService : IPrinterDetectionService
{
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

                if (!string.IsNullOrEmpty(name))
                {
                    printers.Add(new InstalledPrinterDto
                    {
                        PrinterName = name,
                        PrinterPath = network == "True" ? name : null,
                        IsDefault = name == defaultPrinter
                    });
                }
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error detecting printers: {ex.Message}");
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
            Console.WriteLine($"Error getting default printer: {ex.Message}");
        }

        return null;
    }
}
