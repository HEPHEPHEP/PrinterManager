namespace PrinterManager.Shared.Models;

public class ApplicationUser
{
    public int Id { get; set; }
    public required string Username { get; set; }
    public string? Email { get; set; }
    public required string PasswordHash { get; set; }
    public string? FullName { get; set; }
    public bool IsActive { get; set; } = true;
    public bool IsLdapUser { get; set; } = false;
    public UserRole Role { get; set; } = UserRole.User;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? LastLogin { get; set; }
}

public enum UserRole
{
    User,
    Administrator
}
