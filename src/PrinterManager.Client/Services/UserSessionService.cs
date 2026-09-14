using Microsoft.Extensions.Logging;
using System.Management;
using System.Runtime.Versioning;

namespace PrinterManager.Client.Services;

public interface IUserSessionService
{
    string GetLoggedInUser();
}

[SupportedOSPlatform("windows")]
public class UserSessionService : IUserSessionService
{
    private static readonly string[] SystemAccountMarkers =
    {
        "SYSTEM", "LOCAL SERVICE", "NETWORK SERVICE", "DWM-", "UMFD-"
    };

    private readonly ILogger<UserSessionService> _logger;

    public UserSessionService(ILogger<UserSessionService> logger)
    {
        _logger = logger;
    }

    public string GetLoggedInUser()
    {
        return GetConsoleUser() ?? GetInteractiveUser() ?? Environment.UserName;
    }

    private string? GetConsoleUser()
    {
        try
        {
            using var searcher = new ManagementObjectSearcher("SELECT UserName FROM Win32_ComputerSystem");
            using var results = searcher.Get();

            foreach (ManagementObject mo in results)
            {
                using (mo)
                {
                    var username = mo["UserName"]?.ToString();
                    if (!string.IsNullOrEmpty(username))
                        return username;
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Angemeldeter Benutzer konnte nicht über Win32_ComputerSystem ermittelt werden");
        }

        return null;
    }

    private string? GetInteractiveUser()
    {
        try
        {
            using var searcher = new ManagementObjectSearcher("SELECT * FROM Win32_LoggedOnUser");
            using var results = searcher.Get();

            foreach (ManagementObject mo in results)
            {
                using (mo)
                {
                    if (mo["Antecedent"] is not ManagementBaseObject antecedent)
                        continue;

                    using (antecedent)
                    {
                        var domain = antecedent["Domain"]?.ToString();
                        var name = antecedent["Name"]?.ToString();

                        if (string.IsNullOrEmpty(domain) || string.IsNullOrEmpty(name))
                            continue;

                        var fullName = $@"{domain}\{name}";

                        // Computer- und Dienstkonten überspringen
                        if (name.EndsWith('$'))
                            continue;

                        if (SystemAccountMarkers.Any(marker =>
                                fullName.Contains(marker, StringComparison.OrdinalIgnoreCase)))
                            continue;

                        return fullName;
                    }
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Interaktiver Benutzer konnte nicht ermittelt werden");
        }

        return null;
    }
}
