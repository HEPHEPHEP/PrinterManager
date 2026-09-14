using Microsoft.Extensions.Logging;
using System.Diagnostics;
using System.Text;

namespace PrinterManager.Client.Services;

public interface IPrinterManagementService
{
    Task<bool> InstallPrinterAsync(string sharePath, CancellationToken cancellationToken = default);
    Task<bool> RemovePrinterAsync(string printerName, CancellationToken cancellationToken = default);
    Task<bool> SetDefaultPrinterAsync(string printerName, string? sharePath, CancellationToken cancellationToken = default);
}

public class PrinterManagementService : IPrinterManagementService
{
    private static readonly TimeSpan ScriptTimeout = TimeSpan.FromMinutes(2);

    private readonly ILogger<PrinterManagementService> _logger;

    public PrinterManagementService(ILogger<PrinterManagementService> logger)
    {
        _logger = logger;
    }

    public async Task<bool> InstallPrinterAsync(string sharePath, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(sharePath))
        {
            _logger.LogWarning("Installation übersprungen: kein Freigabepfad angegeben");
            return false;
        }

        _logger.LogInformation("Installiere Drucker von Freigabe {SharePath}", sharePath);

        var script = $"Add-Printer -ConnectionName {Quote(sharePath)} -ErrorAction Stop";
        var result = await ExecutePowerShellAsync(script, cancellationToken);

        if (result)
            _logger.LogInformation("Drucker installiert: {SharePath}", sharePath);
        else
            _logger.LogWarning("Installation fehlgeschlagen: {SharePath}", sharePath);

        return result;
    }

    public async Task<bool> RemovePrinterAsync(string printerName, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(printerName))
        {
            _logger.LogWarning("Entfernen übersprungen: kein Druckername angegeben");
            return false;
        }

        _logger.LogInformation("Entferne Drucker {PrinterName}", printerName);

        var script = $"Remove-Printer -Name {Quote(printerName)} -ErrorAction Stop";
        var result = await ExecutePowerShellAsync(script, cancellationToken);

        if (result)
            _logger.LogInformation("Drucker entfernt: {PrinterName}", printerName);
        else
            _logger.LogWarning("Entfernen fehlgeschlagen: {PrinterName}", printerName);

        return result;
    }

    public async Task<bool> SetDefaultPrinterAsync(
        string printerName, string? sharePath, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(printerName) && string.IsNullOrWhiteSpace(sharePath))
        {
            _logger.LogWarning("Standarddrucker übersprungen: weder Name noch Freigabepfad angegeben");
            return false;
        }

        _logger.LogInformation("Setze Standarddrucker {PrinterName} ({SharePath})", printerName, sharePath);

        // Netzwerkdrucker heißen unter Windows in der Regel wie ihr UNC-Pfad, der in der
        // Verwaltung hinterlegte Anzeigename kann davon abweichen — deshalb beide prüfen.
        var script = $$"""
            $candidates = @({{Quote(sharePath ?? string.Empty)}}, {{Quote(printerName)}}) |
                Where-Object { $_ }
            $printer = Get-CimInstance -ClassName Win32_Printer |
                Where-Object { $candidates -contains $_.Name } |
                Select-Object -First 1
            if (-not $printer) {
                Write-Error "Drucker nicht gefunden"
                exit 1
            }
            Invoke-CimMethod -InputObject $printer -MethodName SetDefaultPrinter -ErrorAction Stop | Out-Null
            """;

        var result = await ExecutePowerShellAsync(script, cancellationToken);

        if (result)
            _logger.LogInformation("Standarddrucker gesetzt: {PrinterName}", printerName);
        else
            _logger.LogWarning("Standarddrucker konnte nicht gesetzt werden: {PrinterName}", printerName);

        return result;
    }

    /// <summary>
    /// Bettet einen Wert als PowerShell-Literal in einfachen Anführungszeichen ein.
    /// Ohne das Verdoppeln des Apostrophs könnte ein Druckername wie
    /// <c>x'; Invoke-Expression ...; '</c> beliebigen Code ausführen.
    /// </summary>
    internal static string Quote(string value) => "'" + (value ?? string.Empty).Replace("'", "''") + "'";

    private async Task<bool> ExecutePowerShellAsync(string script, CancellationToken cancellationToken)
    {
        // -EncodedCommand umgeht das Quoting der Kommandozeile vollständig; damit können
        // Anführungszeichen, Zeilenumbrüche und Sonderzeichen im Skript nichts kaputt machen.
        var encoded = Convert.ToBase64String(Encoding.Unicode.GetBytes(script));

        var startInfo = new ProcessStartInfo
        {
            FileName = "powershell.exe",
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true
        };
        startInfo.ArgumentList.Add("-NoProfile");
        startInfo.ArgumentList.Add("-NonInteractive");
        startInfo.ArgumentList.Add("-ExecutionPolicy");
        startInfo.ArgumentList.Add("Bypass");
        startInfo.ArgumentList.Add("-EncodedCommand");
        startInfo.ArgumentList.Add(encoded);

        using var timeoutSource = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeoutSource.CancelAfter(ScriptTimeout);

        try
        {
            using var process = Process.Start(startInfo);
            if (process == null)
            {
                _logger.LogError("PowerShell-Prozess konnte nicht gestartet werden");
                return false;
            }

            // Erst lesen, dann warten: umgekehrt blockiert der Prozess, sobald er den
            // Pipe-Puffer füllt, und WaitForExit kehrt nie zurück.
            var outputTask = process.StandardOutput.ReadToEndAsync();
            var errorTask = process.StandardError.ReadToEndAsync();

            try
            {
                await process.WaitForExitAsync(timeoutSource.Token);
            }
            catch (OperationCanceledException)
            {
                TryKill(process);

                if (cancellationToken.IsCancellationRequested)
                    throw;

                _logger.LogError("PowerShell-Skript nach {Timeout} abgebrochen", ScriptTimeout);
                return false;
            }

            var output = await outputTask;
            var error = await errorTask;

            if (!string.IsNullOrWhiteSpace(output))
            {
                _logger.LogDebug("PowerShell-Ausgabe: {Output}", output.Trim());
            }

            if (process.ExitCode != 0)
            {
                _logger.LogError("PowerShell-Skript mit Exit-Code {ExitCode} beendet. Fehler: {Error}",
                    process.ExitCode, error.Trim());
                return false;
            }

            if (!string.IsNullOrWhiteSpace(error))
            {
                _logger.LogWarning("PowerShell-Fehlerausgabe: {Error}", error.Trim());
            }

            return true;
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Fehler beim Ausführen des PowerShell-Skripts");
            return false;
        }
    }

    private void TryKill(Process process)
    {
        try
        {
            if (!process.HasExited)
                process.Kill(entireProcessTree: true);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "PowerShell-Prozess konnte nicht beendet werden");
        }
    }
}
