using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using PrinterManager.Server.Data;
using PrinterManager.Shared.DTOs;
using PrinterManager.Shared.Models;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;

namespace PrinterManager.Server.Services;

public interface IAuthenticationService
{
    Task<LoginResponseDto> LoginAsync(LoginDto dto);
    Task<UserDto> RegisterUserAsync(RegisterUserDto dto);
    Task<List<UserDto>> GetAllUsersAsync();
    Task<bool> DeleteUserAsync(int id);
    Task<UserDto?> UpdateUserAsync(int id, RegisterUserDto dto);
    Task<UserDto?> UpdateUserRoleAsync(int id, UpdateUserRoleDto dto);
}

public class AuthenticationService : IAuthenticationService
{
    private readonly PrinterManagerDbContext _context;
    private readonly ILdapService _ldapService;
    private readonly IConfiguration _configuration;

    public AuthenticationService(
        PrinterManagerDbContext context,
        ILdapService ldapService,
        IConfiguration configuration)
    {
        _context = context;
        _ldapService = ldapService;
        _configuration = configuration;
    }

    public async Task<LoginResponseDto> LoginAsync(LoginDto dto)
    {
        // Try LDAP authentication if enabled
        if (await _ldapService.IsEnabledAsync())
        {
            var ldapAuth = await _ldapService.AuthenticateAsync(dto.Username, dto.Password);
            if (ldapAuth.Success)
            {
                // Create or update LDAP user in database
                var ldapUser = await GetOrCreateLdapUserAsync(dto.Username, ldapAuth.FullName, ldapAuth.Email);
                ldapUser.LastLogin = DateTime.UtcNow;
                await _context.SaveChangesAsync();

                var token = GenerateJwtToken(ldapUser);
                return new LoginResponseDto
                {
                    Success = true,
                    Token = token,
                    Username = ldapUser.Username,
                    Role = ldapUser.Role.ToString()
                };
            }
            // If LDAP is enabled but auth failed, don't try local auth
            return new LoginResponseDto
            {
                Success = false,
                Message = "LDAP-Authentifizierung fehlgeschlagen"
            };
        }

        // Local authentication
        var user = await _context.ApplicationUsers
            .FirstOrDefaultAsync(u => u.Username == dto.Username && u.IsActive);

        if (user == null)
        {
            return new LoginResponseDto
            {
                Success = false,
                Message = "Ungültiger Benutzername oder Passwort"
            };
        }

        if (!VerifyPassword(dto.Password, user.PasswordHash))
        {
            return new LoginResponseDto
            {
                Success = false,
                Message = "Ungültiger Benutzername oder Passwort"
            };
        }

        user.LastLogin = DateTime.UtcNow;
        await _context.SaveChangesAsync();

        var jwtToken = GenerateJwtToken(user);
        return new LoginResponseDto
        {
            Success = true,
            Token = jwtToken,
            Username = user.Username,
            Role = user.Role.ToString()
        };
    }

    public async Task<UserDto> RegisterUserAsync(RegisterUserDto dto)
    {
        if (await _context.ApplicationUsers.AnyAsync(u => u.Username == dto.Username))
        {
            throw new InvalidOperationException("Benutzername bereits vergeben");
        }

        var user = new ApplicationUser
        {
            Username = dto.Username,
            Email = dto.Email,
            FullName = dto.FullName,
            PasswordHash = HashPassword(dto.Password),
            Role = Enum.Parse<UserRole>(dto.Role),
            IsLdapUser = false
        };

        _context.ApplicationUsers.Add(user);
        await _context.SaveChangesAsync();

        return new UserDto
        {
            Id = user.Id,
            Username = user.Username,
            Email = user.Email,
            FullName = user.FullName,
            IsActive = user.IsActive,
            IsLdapUser = user.IsLdapUser,
            Role = user.Role.ToString()
        };
    }

    public async Task<List<UserDto>> GetAllUsersAsync()
    {
        return await _context.ApplicationUsers
            .Select(u => new UserDto
            {
                Id = u.Id,
                Username = u.Username,
                Email = u.Email,
                FullName = u.FullName,
                IsActive = u.IsActive,
                IsLdapUser = u.IsLdapUser,
                Role = u.Role.ToString(),
                LastLogin = u.LastLogin
            })
            .ToListAsync();
    }

    public async Task<bool> DeleteUserAsync(int id)
    {
        var user = await _context.ApplicationUsers.FindAsync(id);
        if (user == null)
            return false;

        _context.ApplicationUsers.Remove(user);
        await _context.SaveChangesAsync();
        return true;
    }

    public async Task<UserDto?> UpdateUserAsync(int id, RegisterUserDto dto)
    {
        var user = await _context.ApplicationUsers.FindAsync(id);
        if (user == null)
            return null;

        user.Email = dto.Email;
        user.FullName = dto.FullName;
        if (!string.IsNullOrEmpty(dto.Password))
        {
            user.PasswordHash = HashPassword(dto.Password);
        }
        user.Role = Enum.Parse<UserRole>(dto.Role);

        await _context.SaveChangesAsync();

        return new UserDto
        {
            Id = user.Id,
            Username = user.Username,
            Email = user.Email,
            FullName = user.FullName,
            IsActive = user.IsActive,
            IsLdapUser = user.IsLdapUser,
            Role = user.Role.ToString()
        };
    }

    public async Task<UserDto?> UpdateUserRoleAsync(int id, UpdateUserRoleDto dto)
    {
        var user = await _context.ApplicationUsers.FindAsync(id);
        if (user == null)
            return null;

        user.Role = Enum.Parse<UserRole>(dto.Role);
        await _context.SaveChangesAsync();

        return new UserDto
        {
            Id = user.Id,
            Username = user.Username,
            Email = user.Email,
            FullName = user.FullName,
            IsActive = user.IsActive,
            IsLdapUser = user.IsLdapUser,
            Role = user.Role.ToString()
        };
    }

    private async Task<ApplicationUser> GetOrCreateLdapUserAsync(string username, string? fullName, string? email)
    {
        var user = await _context.ApplicationUsers
            .FirstOrDefaultAsync(u => u.Username == username);

        if (user == null)
        {
            user = new ApplicationUser
            {
                Username = username,
                Email = email,
                FullName = fullName,
                PasswordHash = string.Empty,
                IsLdapUser = true,
                Role = UserRole.User
            };
            _context.ApplicationUsers.Add(user);
        }
        else
        {
            user.Email = email;
            user.FullName = fullName;
        }

        return user;
    }

    private string GenerateJwtToken(ApplicationUser user)
    {
        var jwtKey = _configuration["Jwt:Key"] ?? "YourSuperSecretKeyThatIsAtLeast32CharactersLong!";
        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtKey));
        var credentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

        var claims = new[]
        {
            new Claim(ClaimTypes.NameIdentifier, user.Id.ToString()),
            new Claim(ClaimTypes.Name, user.Username),
            new Claim(ClaimTypes.Role, user.Role.ToString())
        };

        var token = new JwtSecurityToken(
            issuer: _configuration["Jwt:Issuer"] ?? "PrinterManager",
            audience: _configuration["Jwt:Audience"] ?? "PrinterManager",
            claims: claims,
            expires: DateTime.UtcNow.AddHours(8),
            signingCredentials: credentials
        );

        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    private static string HashPassword(string password)
    {
        using var sha256 = SHA256.Create();
        var hashedBytes = sha256.ComputeHash(Encoding.UTF8.GetBytes(password));
        return Convert.ToBase64String(hashedBytes);
    }

    private static bool VerifyPassword(string password, string hash)
    {
        var passwordHash = HashPassword(password);
        return passwordHash == hash;
    }
}
