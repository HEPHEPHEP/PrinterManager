using Microsoft.Win32;

namespace PrinterManager.Client.Services;

public interface IAutostartService
{
    bool IsAutostartEnabled();
    void EnableAutostart();
    void DisableAutostart();
}

public class AutostartService : IAutostartService
{
    private const string AppName = "PrinterManagerClient";
    private const string RunKeyPath = @"SOFTWARE\Microsoft\Windows\CurrentVersion\Run";
    private readonly ILogger<AutostartService> _logger;

    public AutostartService(ILogger<AutostartService> logger)
    {
        _logger = logger;
    }

    public bool IsAutostartEnabled()
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(RunKeyPath, false);
            var value = key?.GetValue(AppName);
            return value != null;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error checking autostart status");
            return false;
        }
    }

    public void EnableAutostart()
    {
        try
        {
            var exePath = Environment.ProcessPath;
            if (string.IsNullOrEmpty(exePath))
            {
                _logger.LogError("Could not determine executable path");
                return;
            }

            using var key = Registry.CurrentUser.OpenSubKey(RunKeyPath, true);
            if (key != null)
            {
                key.SetValue(AppName, $"\"{exePath}\"");
                _logger.LogInformation($"Autostart enabled: {exePath}");
                _logger.LogInformation($"The PrinterManager Client will now start automatically when user {Environment.UserName} logs in");
            }
            else
            {
                _logger.LogError("Could not open registry key for writing");
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error enabling autostart");
        }
    }

    public void DisableAutostart()
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(RunKeyPath, true);
            if (key != null)
            {
                key.DeleteValue(AppName, false);
                _logger.LogInformation("Autostart disabled");
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error disabling autostart");
        }
    }
}
