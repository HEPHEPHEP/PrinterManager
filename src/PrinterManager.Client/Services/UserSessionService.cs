using System.Management;

namespace PrinterManager.Client.Services;

public interface IUserSessionService
{
    string GetLoggedInUser();
}

public class UserSessionService : IUserSessionService
{
    public string GetLoggedInUser()
    {
        try
        {
            // Query for logged-in users using WMI
            using var searcher = new ManagementObjectSearcher(
                "SELECT UserName FROM Win32_ComputerSystem");

            foreach (ManagementObject mo in searcher.Get())
            {
                var username = mo["UserName"]?.ToString();
                if (!string.IsNullOrEmpty(username))
                {
                    return username;
                }
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error getting logged-in user: {ex.Message}");
        }

        // Fallback: try to get interactive session user
        try
        {
            using var searcher = new ManagementObjectSearcher(
                "SELECT * FROM Win32_LoggedOnUser");

            foreach (ManagementObject mo in searcher.Get())
            {
                var dependent = (ManagementBaseObject)mo["Dependent"];
                var antecedent = (ManagementBaseObject)mo["Antecedent"];

                if (dependent != null && antecedent != null)
                {
                    var domain = antecedent["Domain"]?.ToString();
                    var name = antecedent["Name"]?.ToString();

                    if (!string.IsNullOrEmpty(domain) && !string.IsNullOrEmpty(name))
                    {
                        var fullName = $"{domain}\\{name}";

                        // Skip system accounts
                        if (!name.EndsWith("$") &&
                            !fullName.Contains("SYSTEM") &&
                            !fullName.Contains("LOCAL SERVICE") &&
                            !fullName.Contains("NETWORK SERVICE") &&
                            !fullName.Contains("DWM-"))
                        {
                            return fullName;
                        }
                    }
                }
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error getting interactive user: {ex.Message}");
        }

        // Last fallback
        return Environment.UserName;
    }
}
