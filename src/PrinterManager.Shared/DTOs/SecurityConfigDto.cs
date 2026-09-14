namespace PrinterManager.Shared.DTOs;

public class LdapConfigDto
{
    public bool Enabled { get; set; }
    public required string Server { get; set; }
    public int Port { get; set; }
    public required string BaseDn { get; set; }
    public required string UserDnTemplate { get; set; }
}

public class UpdateUserRoleDto
{
    public required string Role { get; set; }
}
