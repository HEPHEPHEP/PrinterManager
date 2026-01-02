using PrinterManager.Shared.DTOs;
using System.Management;

namespace PrinterManager.Server.Services;

public interface IPrintServerScanService
{
    Task<List<ScannedPrinterDto>> ScanPrintServerAsync(PrintServerScanDto dto);
}

public class PrintServerScanService : IPrintServerScanService
{
    public async Task<List<ScannedPrinterDto>> ScanPrintServerAsync(PrintServerScanDto dto)
    {
        return await Task.Run(() =>
        {
            var printers = new List<ScannedPrinterDto>();

            try
            {
                // Note: This requires Windows and appropriate permissions
                var scope = new ManagementScope($"\\\\{dto.ServerName}\\root\\cimv2");

                if (!string.IsNullOrEmpty(dto.Username) && !string.IsNullOrEmpty(dto.Password))
                {
                    var options = new ConnectionOptions
                    {
                        Username = dto.Username,
                        Password = dto.Password
                    };
                    scope.Options = options;
                }

                scope.Connect();

                var query = new ObjectQuery("SELECT * FROM Win32_Printer WHERE Shared = True");
                var searcher = new ManagementObjectSearcher(scope, query);

                foreach (ManagementObject printer in searcher.Get())
                {
                    var shareName = printer["ShareName"]?.ToString();
                    var name = printer["Name"]?.ToString();

                    if (!string.IsNullOrEmpty(shareName) && !string.IsNullOrEmpty(name))
                    {
                        printers.Add(new ScannedPrinterDto
                        {
                            Name = name,
                            ShareName = shareName,
                            SharePath = $"\\\\{dto.ServerName}\\{shareName}",
                            Location = printer["Location"]?.ToString(),
                            Comment = printer["Comment"]?.ToString(),
                            DriverName = printer["DriverName"]?.ToString()
                        });
                    }
                }
            }
            catch (Exception ex)
            {
                // Log exception
                Console.WriteLine($"Error scanning print server: {ex.Message}");
            }

            return printers;
        });
    }
}
