using System.DirectoryServices.Protocols;
using System.Net;

namespace PrinterManager.Server.Services;

public interface ILdapService
{
    Task<LdapAuthResult> AuthenticateAsync(string username, string password);
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
    private readonly IConfiguration _configuration;

    public LdapService(IConfiguration configuration)
    {
        _configuration = configuration;
    }

    public async Task<LdapAuthResult> AuthenticateAsync(string username, string password)
    {
        return await Task.Run(() =>
        {
            if (!_configuration.GetValue<bool>("Ldap:Enabled"))
            {
                return new LdapAuthResult
                {
                    Success = false,
                    Message = "LDAP is not enabled"
                };
            }

            try
            {
                var server = _configuration["Ldap:Server"];
                var port = _configuration.GetValue<int>("Ldap:Port", 389);
                var baseDn = _configuration["Ldap:BaseDn"];
                var userDnTemplate = _configuration["Ldap:UserDnTemplate"] ?? "uid={0}," + baseDn;

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
