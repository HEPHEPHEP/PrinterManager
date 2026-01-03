namespace PrinterManager.Shared.DTOs;

public class LoginDto
{
    public required string Username { get; set; }
    public required string Password { get; set; }
    public bool UseLdap { get; set; }
}

public class LoginResponseDto
{
    public bool Success { get; set; }
    public string? Token { get; set; }
    public string? Username { get; set; }
    public string? Role { get; set; }
    public string? Message { get; set; }
}

public class RegisterUserDto
{
    public required string Username { get; set; }
    public required string Password { get; set; }
    public string? Email { get; set; }
    public string? FullName { get; set; }
    public string Role { get; set; } = "User";
}

public class UserDto
{
    public int Id { get; set; }
    public required string Username { get; set; }
    public string? Email { get; set; }
    public string? FullName { get; set; }
    public bool IsActive { get; set; }
    public bool IsLdapUser { get; set; }
    public string Role { get; set; } = string.Empty;
    public DateTime? LastLogin { get; set; }
}
