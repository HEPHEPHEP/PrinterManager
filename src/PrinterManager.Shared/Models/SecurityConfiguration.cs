namespace PrinterManager.Shared.Models;

public class LdapConfiguration
{
    public int Id { get; set; }
    public bool Enabled { get; set; }
    public string Server { get; set; } = "ldap.example.com";
    public int Port { get; set; } = 389;
    public string BaseDn { get; set; } = "dc=example,dc=com";
    public string UserDnTemplate { get; set; } = "uid={0},ou=users,dc=example,dc=com";
    public DateTime LastModified { get; set; } = DateTime.UtcNow;
}

public class SslConfiguration
{
    public int Id { get; set; }
    public bool Enabled { get; set; }
    public int HttpsPort { get; set; } = 5443;
    public string? CertificatePath { get; set; }
    public string? CertificatePassword { get; set; }
    public DateTime LastModified { get; set; } = DateTime.UtcNow;
}
