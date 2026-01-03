using Microsoft.EntityFrameworkCore;
using PrinterManager.Server.Data;
using System.DirectoryServices.Protocols;
using System.Net;

namespace PrinterManager.Server.Services;

public interface ILdapService
{
    Task<LdapAuthResult> AuthenticateAsync(string username, string password);
    Task<bool> IsEnabledAsync();
}

public class LdapAuthResult
{
    public bool Success { get; set; }
    public string? FullName { get; set; }
    public string? Email { get; set; }
    public string? Message { get; set; }
}

public class LdapService : ILdapService
{
    private readonly PrinterManagerDbContext _context;

    public LdapService(PrinterManagerDbContext context)
    {
        _context = context;
    }

    public async Task<bool> IsEnabledAsync()
    {
        var config = await _context.LdapConfigurations.FirstOrDefaultAsync();
        return config?.Enabled ?? false;
    }

    public async Task<LdapAuthResult> AuthenticateAsync(string username, string password)
    {
        return await Task.Run(async () =>
        {
            var config = await _context.LdapConfigurations.FirstOrDefaultAsync();

            if (config == null || !config.Enabled)
            {
                return new LdapAuthResult
                {
                    Success = false,
                    Message = "LDAP is not enabled"
                };
            }

            try
            {
                var server = config.Server;
                var port = config.Port;
                var baseDn = config.BaseDn;
                var userDnTemplate = config.UserDnTemplate;

                if (string.IsNullOrEmpty(server) || string.IsNullOrEmpty(baseDn))
                {
                    return new LdapAuthResult
                    {
                        Success = false,
                        Message = "LDAP configuration is incomplete"
                    };
                }

                var userDn = userDnTemplate.Replace("{0}", username);

                using var connection = new LdapConnection(new LdapDirectoryIdentifier(server, port));
                connection.AuthType = AuthType.Basic;
                connection.Credential = new NetworkCredential(userDn, password);

                // Try to bind - this will throw if authentication fails
                connection.Bind();

                // Search for user details
                var searchRequest = new SearchRequest(
                    baseDn,
                    $"(uid={username})",
                    SearchScope.Subtree,
                    "cn", "mail", "displayName"
                );

                var searchResponse = (SearchResponse)connection.SendRequest(searchRequest);

                string? fullName = null;
                string? email = null;

                if (searchResponse.Entries.Count > 0)
                {
                    var entry = searchResponse.Entries[0];
                    fullName = entry.Attributes["cn"]?[0]?.ToString() ??
                              entry.Attributes["displayName"]?[0]?.ToString();
                    email = entry.Attributes["mail"]?[0]?.ToString();
                }

                return new LdapAuthResult
                {
                    Success = true,
                    FullName = fullName,
                    Email = email
                };
            }
            catch (LdapException ex)
            {
                return new LdapAuthResult
                {
                    Success = false,
                    Message = $"LDAP authentication failed: {ex.Message}"
                };
            }
            catch (Exception ex)
            {
                return new LdapAuthResult
                {
                    Success = false,
                    Message = $"Authentication error: {ex.Message}"
                };
            }
        });
    }
}
