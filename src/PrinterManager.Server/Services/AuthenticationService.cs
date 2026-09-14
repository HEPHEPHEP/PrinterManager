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
    /// <summary>Mindestlänge für lokale Passwörter.</summary>
    public const int MinimumPasswordLength = 8;

    private const int BcryptWorkFactor = 12;

    /// <summary>
    /// BCrypt-Hash eines Zufallswerts. Wird für unbekannte Benutzer verifiziert, damit die
    /// Antwortzeit keine Rückschlüsse auf existierende Benutzernamen zulässt.
    /// </summary>
    private static readonly string DummyHash =
        BCrypt.Net.BCrypt.HashPassword(Guid.NewGuid().ToString(), BcryptWorkFactor);

    private readonly PrinterManagerDbContext _context;
    private readonly ILdapService _ldapService;
    private readonly IConfiguration _configuration;
    private readonly ILogger<AuthenticationService> _logger;

    public AuthenticationService(
        PrinterManagerDbContext context,
        ILdapService ldapService,
        IConfiguration configuration,
        ILogger<AuthenticationService> logger)
    {
        _context = context;
        _ldapService = ldapService;
        _configuration = configuration;
        _logger = logger;
    }

    public async Task<LoginResponseDto> LoginAsync(LoginDto dto)
    {
        var invalidCredentials = new LoginResponseDto
        {
            Success = false,
            Message = "Ungültiger Benutzername oder Passwort"
        };

        if (string.IsNullOrWhiteSpace(dto.Username) || string.IsNullOrEmpty(dto.Password))
        {
            return invalidCredentials;
        }

        if (await _ldapService.IsEnabledAsync())
        {
            var ldapAuth = await _ldapService.AuthenticateAsync(dto.Username, dto.Password);
            if (ldapAuth.Success)
            {
                var ldapUser = await GetOrCreateLdapUserAsync(dto.Username, ldapAuth.FullName, ldapAuth.Email);

                if (!ldapUser.IsActive)
                {
                    _logger.LogInformation("Login für deaktivierten LDAP-Benutzer {Username} abgelehnt", dto.Username);
                    return invalidCredentials;
                }

                ldapUser.LastLogin = DateTime.UtcNow;
                await _context.SaveChangesAsync();

                return new LoginResponseDto
                {
                    Success = true,
                    Token = GenerateJwtToken(ldapUser),
                    Username = ldapUser.Username,
                    Role = ldapUser.Role.ToString()
                };
            }

            // Fällt bewusst auf die lokale Anmeldung zurück: sonst sperrt eine fehlerhafte
            // LDAP-Konfiguration auch den lokalen Administrator aus. LDAP-Benutzer selbst
            // haben keinen lokalen Hash und können sich hier nicht anmelden.
            _logger.LogInformation(
                "LDAP-Anmeldung für {Username} fehlgeschlagen — versuche lokale Anmeldung", dto.Username);
        }

        var user = await _context.ApplicationUsers
            .FirstOrDefaultAsync(u => u.Username == dto.Username && u.IsActive && !u.IsLdapUser);

        if (user == null || string.IsNullOrEmpty(user.PasswordHash))
        {
            // Gleiche Arbeit wie bei einem existierenden Benutzer, um Timing-Angriffe
            // zur Benutzernamen-Erkennung zu vermeiden.
            BCrypt.Net.BCrypt.Verify(dto.Password, DummyHash);
            return invalidCredentials;
        }

        if (!VerifyPassword(dto.Password, user.PasswordHash))
        {
            return invalidCredentials;
        }

        // Automatische Hash-Migration: SHA256 -> BCrypt beim nächsten Login
        if (!IsBcryptHash(user.PasswordHash))
        {
            user.PasswordHash = HashPassword(dto.Password);
            _logger.LogInformation("Passwort-Hash für {Username} auf BCrypt migriert", user.Username);
        }

        user.LastLogin = DateTime.UtcNow;
        await _context.SaveChangesAsync();

        return new LoginResponseDto
        {
            Success = true,
            Token = GenerateJwtToken(user),
            Username = user.Username,
            Role = user.Role.ToString()
        };
    }

    public async Task<UserDto> RegisterUserAsync(RegisterUserDto dto)
    {
        if (string.IsNullOrWhiteSpace(dto.Username))
        {
            throw new ArgumentException("Benutzername ist erforderlich.");
        }

        var role = ParseRole(dto.Role);
        ValidatePassword(dto.Password);

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
            Role = role,
            IsLdapUser = false
        };

        _context.ApplicationUsers.Add(user);
        await _context.SaveChangesAsync();

        return ToDto(user);
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

        await EnsureNotLastAdministratorAsync(user, "Der letzte Administrator kann nicht gelöscht werden.");

        _context.ApplicationUsers.Remove(user);
        await _context.SaveChangesAsync();
        return true;
    }

    public async Task<UserDto?> UpdateUserAsync(int id, RegisterUserDto dto)
    {
        var user = await _context.ApplicationUsers.FindAsync(id);
        if (user == null)
            return null;

        var role = ParseRole(dto.Role);

        if (!string.IsNullOrEmpty(dto.Password))
        {
            ValidatePassword(dto.Password);
        }

        if (role != UserRole.Administrator)
        {
            await EnsureNotLastAdministratorAsync(user,
                "Dem letzten Administrator können die Rechte nicht entzogen werden.");
        }

        user.Email = dto.Email;
        user.FullName = dto.FullName;
        if (!string.IsNullOrEmpty(dto.Password))
        {
            user.PasswordHash = HashPassword(dto.Password);
        }
        user.Role = role;

        await _context.SaveChangesAsync();

        return ToDto(user);
    }

    public async Task<UserDto?> UpdateUserRoleAsync(int id, UpdateUserRoleDto dto)
    {
        var user = await _context.ApplicationUsers.FindAsync(id);
        if (user == null)
            return null;

        var role = ParseRole(dto.Role);

        if (role != UserRole.Administrator)
        {
            await EnsureNotLastAdministratorAsync(user,
                "Dem letzten Administrator können die Rechte nicht entzogen werden.");
        }

        user.Role = role;
        await _context.SaveChangesAsync();

        return ToDto(user);
    }

    private async Task EnsureNotLastAdministratorAsync(ApplicationUser user, string message)
    {
        if (user.Role != UserRole.Administrator)
            return;

        var otherAdmins = await _context.ApplicationUsers
            .CountAsync(u => u.Role == UserRole.Administrator && u.IsActive && u.Id != user.Id);

        if (otherAdmins == 0)
        {
            throw new InvalidOperationException(message);
        }
    }

    private static UserRole ParseRole(string? role)
    {
        if (string.IsNullOrWhiteSpace(role))
            return UserRole.User;

        if (!Enum.TryParse<UserRole>(role, ignoreCase: true, out var parsed) || !Enum.IsDefined(parsed))
        {
            throw new ArgumentException(
                $"Ungültige Rolle '{role}'. Erlaubt: {string.Join(", ", Enum.GetNames<UserRole>())}.");
        }

        return parsed;
    }

    private static void ValidatePassword(string? password)
    {
        if (string.IsNullOrWhiteSpace(password) || password.Length < MinimumPasswordLength)
        {
            throw new ArgumentException($"Das Passwort muss mindestens {MinimumPasswordLength} Zeichen lang sein.");
        }
    }

    private static UserDto ToDto(ApplicationUser user) => new()
    {
        Id = user.Id,
        Username = user.Username,
        Email = user.Email,
        FullName = user.FullName,
        IsActive = user.IsActive,
        IsLdapUser = user.IsLdapUser,
        Role = user.Role.ToString(),
        LastLogin = user.LastLogin
    };

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
        var jwtKey = _configuration["Jwt:Key"]
            ?? throw new InvalidOperationException(
                "JWT-Key nicht konfiguriert. Bitte Jwt:Key in appsettings.json oder Umgebungsvariable setzen.");
        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtKey));
        var credentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

        var claims = new[]
        {
            new Claim(ClaimTypes.NameIdentifier, user.Id.ToString()),
            new Claim(ClaimTypes.Name, user.Username),
            new Claim(ClaimTypes.Role, user.Role.ToString()),
            new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString())
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

    private static bool IsBcryptHash(string hash) => hash.StartsWith("$2", StringComparison.Ordinal);

    private static string HashPassword(string password)
    {
        return BCrypt.Net.BCrypt.HashPassword(password, BcryptWorkFactor);
    }

    private static bool VerifyPassword(string password, string hash)
    {
        // Abwärtskompatibilität: alte SHA256-Hashes erkennen und beim Login migrieren
        // (siehe LoginAsync). Neue Hashes werden immer mit BCrypt erzeugt.
        if (!IsBcryptHash(hash))
        {
            var computed = SHA256.HashData(Encoding.UTF8.GetBytes(password));
            byte[] stored;
            try
            {
                stored = Convert.FromBase64String(hash);
            }
            catch (FormatException)
            {
                return false;
            }

            return CryptographicOperations.FixedTimeEquals(computed, stored);
        }

        try
        {
            return BCrypt.Net.BCrypt.Verify(password, hash);
        }
        catch (BCrypt.Net.SaltParseException)
        {
            return false;
        }
    }
}
