namespace PrinterManager.Shared.Models;

public class LdapConfiguration
{
    public int Id { get; set; }
    public bool Enabled { get; set; }
    public string Server { get; set; } = "";
    public int Port { get; set; } = 389;
    public string BaseDn { get; set; } = "";
    public string UserDnTemplate { get; set; } = "";
    public DateTime LastModified { get; set; } = DateTime.UtcNow;
}
