using PrinterManager.Shared.DTOs;
using System.Management;
using System.Runtime.Versioning;
using System.Text.RegularExpressions;

namespace PrinterManager.Server.Services;

public interface IPrintServerScanService
{
    Task<List<ScannedPrinterDto>> ScanPrintServerAsync(PrintServerScanDto dto);
}

public partial class PrintServerScanService : IPrintServerScanService
{
    private readonly ILogger<PrintServerScanService> _logger;

    public PrintServerScanService(ILogger<PrintServerScanService> logger)
    {
        _logger = logger;
    }

    [SupportedOSPlatform("windows")]
    public async Task<List<ScannedPrinterDto>> ScanPrintServerAsync(PrintServerScanDto dto)
    {
        var serverName = dto.ServerName?.Trim().TrimStart('\\') ?? string.Empty;

        // Der Name landet in einem WMI-Pfad — nur echte Hostnamen zulassen.
        if (!HostnamePattern().IsMatch(serverName))
        {
            throw new ArgumentException(
                "Ungültiger Servername. Erlaubt sind Buchstaben, Ziffern, '.', '-' und '_'.");
        }

        if (!OperatingSystem.IsWindows())
        {
            throw new InvalidOperationException(
                "Das Scannen von Druckservern wird nur unter Windows unterstützt (WMI).");
        }

        return await Task.Run(() => Scan(serverName, dto.Username, dto.Password));
    }

    [SupportedOSPlatform("windows")]
    private List<ScannedPrinterDto> Scan(string serverName, string? username, string? password)
    {
        var printers = new List<ScannedPrinterDto>();

        try
        {
            var scope = new ManagementScope($@"\\{serverName}\root\cimv2");

            if (!string.IsNullOrEmpty(username) && !string.IsNullOrEmpty(password))
            {
                scope.Options = new ConnectionOptions
                {
                    Username = username,
                    Password = password
                };
            }

            scope.Connect();

            var query = new ObjectQuery("SELECT * FROM Win32_Printer WHERE Shared = True");
            using var searcher = new ManagementObjectSearcher(scope, query);
            using var results = searcher.Get();

            foreach (ManagementObject printer in results)
            {
                using (printer)
                {
                    var shareName = printer["ShareName"]?.ToString();
                    var name = printer["Name"]?.ToString();

                    if (string.IsNullOrEmpty(shareName) || string.IsNullOrEmpty(name))
                        continue;

                    printers.Add(new ScannedPrinterDto
                    {
                        Name = name,
                        ShareName = shareName,
                        SharePath = $@"\\{serverName}\{shareName}",
                        Location = printer["Location"]?.ToString(),
                        Comment = printer["Comment"]?.ToString(),
                        DriverName = printer["DriverName"]?.ToString()
                    });
                }
            }

            _logger.LogInformation("Druckserver {Server}: {Count} freigegebene Drucker gefunden",
                serverName, printers.Count);
        }
        catch (ManagementException ex)
        {
            _logger.LogError(ex, "WMI-Fehler beim Scannen von {Server}", serverName);
            throw new InvalidOperationException($"WMI-Abfrage fehlgeschlagen: {ex.Message}", ex);
        }
        catch (UnauthorizedAccessException ex)
        {
            _logger.LogError(ex, "Zugriff auf {Server} verweigert", serverName);
            throw new InvalidOperationException(
                "Zugriff verweigert. Bitte Anmeldedaten für den Druckserver angeben.", ex);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Fehler beim Scannen von {Server}", serverName);
            throw new InvalidOperationException($"Scan fehlgeschlagen: {ex.Message}", ex);
        }

        return printers;
    }

    [GeneratedRegex(@"^[A-Za-z0-9][A-Za-z0-9._-]{0,252}$")]
    private static partial Regex HostnamePattern();
}
