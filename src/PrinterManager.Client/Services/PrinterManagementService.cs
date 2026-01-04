using System.Diagnostics;
using System.Management;
using PrinterManager.Shared.DTOs;
using Microsoft.Extensions.Logging;

namespace PrinterManager.Client.Services;

public interface IPrinterManagementService
{
    Task<bool> InstallPrinterAsync(string sharePath);
    Task<bool> RemovePrinterAsync(string printerName);
    Task<bool> SetDefaultPrinterAsync(string printerName);
}

public class PrinterManagementService : IPrinterManagementService
{
    private readonly ILogger<PrinterManagementService> _logger;

    public PrinterManagementService(ILogger<PrinterManagementService> logger)
    {
        _logger = logger;
    }
    public async Task<bool> InstallPrinterAsync(string sharePath)
    {
        try
        {
            _logger.LogInformation($"Attempting to install printer from share path: {sharePath}");
            var script = $@"
                $printerPath = '{sharePath}'
                Add-Printer -ConnectionName $printerPath -ErrorAction Stop
            ";

            var result = await ExecutePowerShellAsync(script);
            if (result)
            {
                _logger.LogInformation($"Successfully installed printer: {sharePath}");
            }
            else
            {
                _logger.LogWarning($"Failed to install printer: {sharePath}");
            }
            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, $"Error installing printer {sharePath}");
            return false;
        }
    }

    public async Task<bool> RemovePrinterAsync(string printerName)
    {
        try
        {
            _logger.LogInformation($"Attempting to remove printer: {printerName}");
            var script = $@"
                $printerName = '{printerName}'
                Remove-Printer -Name $printerName -ErrorAction Stop
            ";

            var result = await ExecutePowerShellAsync(script);
            if (result)
            {
                _logger.LogInformation($"Successfully removed printer: {printerName}");
            }
            else
            {
                _logger.LogWarning($"Failed to remove printer: {printerName}");
            }
            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, $"Error removing printer {printerName}");
            return false;
        }
    }

    public async Task<bool> SetDefaultPrinterAsync(string printerName)
    {
        try
        {
            _logger.LogInformation($"Attempting to set default printer: {printerName}");
            var script = $@"
                $printerName = '{printerName}'
                $printer = Get-CimInstance -ClassName Win32_Printer | Where-Object {{ $_.Name -eq $printerName }}
                if ($printer) {{
                    Invoke-CimMethod -InputObject $printer -MethodName SetDefaultPrinter
                }}
            ";

            var result = await ExecutePowerShellAsync(script);
            if (result)
            {
                _logger.LogInformation($"Successfully set default printer: {printerName}");
            }
            else
            {
                _logger.LogWarning($"Failed to set default printer: {printerName}");
            }
            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, $"Error setting default printer {printerName}");
            return false;
        }
    }

    private async Task<bool> ExecutePowerShellAsync(string script)
    {
        return await Task.Run(() =>
        {
            try
            {
                var startInfo = new ProcessStartInfo
                {
                    FileName = "powershell.exe",
                    Arguments = $"-NoProfile -ExecutionPolicy Bypass -Command \"{script}\"",
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    CreateNoWindow = true
                };

                using var process = Process.Start(startInfo);
                if (process == null)
                {
                    _logger.LogError("Failed to start PowerShell process");
                    return false;
                }

                process.WaitForExit();
                var output = process.StandardOutput.ReadToEnd();
                var error = process.StandardError.ReadToEnd();

                if (!string.IsNullOrWhiteSpace(output))
                {
                    _logger.LogDebug($"PowerShell output: {output}");
                }

                if (process.ExitCode != 0)
                {
                    _logger.LogError($"PowerShell script failed with exit code {process.ExitCode}. Error: {error}");
                    return false;
                }

                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error executing PowerShell script");
                return false;
            }
        });
    }
}
