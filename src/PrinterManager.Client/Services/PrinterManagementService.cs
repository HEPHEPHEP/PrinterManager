using System.Diagnostics;
using System.Management;
using PrinterManager.Shared.DTOs;

namespace PrinterManager.Client.Services;

public interface IPrinterManagementService
{
    Task<bool> InstallPrinterAsync(string sharePath);
    Task<bool> RemovePrinterAsync(string printerName);
    Task<bool> SetDefaultPrinterAsync(string printerName);
}

public class PrinterManagementService : IPrinterManagementService
{
    public async Task<bool> InstallPrinterAsync(string sharePath)
    {
        try
        {
            var script = $@"
                $printerPath = '{sharePath}'
                Add-Printer -ConnectionName $printerPath -ErrorAction Stop
            ";

            return await ExecutePowerShellAsync(script);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error installing printer {sharePath}: {ex.Message}");
            return false;
        }
    }

    public async Task<bool> RemovePrinterAsync(string printerName)
    {
        try
        {
            var script = $@"
                $printerName = '{printerName}'
                Remove-Printer -Name $printerName -ErrorAction Stop
            ";

            return await ExecutePowerShellAsync(script);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error removing printer {printerName}: {ex.Message}");
            return false;
        }
    }

    public async Task<bool> SetDefaultPrinterAsync(string printerName)
    {
        try
        {
            var script = $@"
                $printerName = '{printerName}'
                $printer = Get-CimInstance -ClassName Win32_Printer | Where-Object {{ $_.Name -eq $printerName }}
                if ($printer) {{
                    Invoke-CimMethod -InputObject $printer -MethodName SetDefaultPrinter
                }}
            ";

            return await ExecutePowerShellAsync(script);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error setting default printer {printerName}: {ex.Message}");
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
                    return false;

                process.WaitForExit();
                var output = process.StandardOutput.ReadToEnd();
                var error = process.StandardError.ReadToEnd();

                if (process.ExitCode != 0)
                {
                    Console.WriteLine($"PowerShell error: {error}");
                    return false;
                }

                return true;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error executing PowerShell: {ex.Message}");
                return false;
            }
        });
    }
}
