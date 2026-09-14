using Microsoft.EntityFrameworkCore;
using PrinterManager.Server.Data;
using PrinterManager.Shared.Models;
using System.Security.Cryptography;

namespace PrinterManager.Server.Setup;

/// <summary>
/// Richtet eine frische Installation ein: Datenbank anlegen und einen Administrator
/// erzeugen. Ohne vorgegebenes Passwort wird eines erzeugt und einmalig ausgegeben.
/// </summary>
public static class FirstRunSetup
{
    public const string PasswordFileName = "initial-admin-password.txt";
    public const string AdminUsername = "admin";
    public const int MinimumPasswordLength = 8;

    public static async Task RunAsync(WebApplication app)
    {
        using var scope = app.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<PrinterManagerDbContext>();

        await db.Database.EnsureCreatedAsync();

        if (await db.ApplicationUsers.AnyAsync(u => u.Role == UserRole.Administrator))
        {
            return;
        }

        var configuredPassword = app.Configuration["AdminPassword"]
            ?? Environment.GetEnvironmentVariable("ADMIN_PASSWORD");

        // Ein selbst gesetztes, aber zu kurzes Passwort ist ein Konfigurationsfehler und
        // wird nicht stillschweigend durch ein erzeugtes ersetzt.
        if (!string.IsNullOrEmpty(configuredPassword) && configuredPassword.Length < MinimumPasswordLength)
        {
            throw new InvalidOperationException(
                $"ADMIN_PASSWORD ist zu kurz (mindestens {MinimumPasswordLength} Zeichen). " +
                "Setze ein längeres Passwort oder lasse die Variable weg, um eines erzeugen zu lassen.");
        }

        var password = configuredPassword ?? GeneratePassword();
        var wasGenerated = configuredPassword == null;

        db.ApplicationUsers.Add(new ApplicationUser
        {
            Username = AdminUsername,
            PasswordHash = BCrypt.Net.BCrypt.HashPassword(password, workFactor: 12),
            IsActive = true,
            IsLdapUser = false,
            Role = UserRole.Administrator,
            CreatedAt = DateTime.UtcNow
        });

        await db.SaveChangesAsync();

        if (!wasGenerated)
        {
            app.Logger.LogInformation(
                "Administrator '{Username}' mit dem Passwort aus ADMIN_PASSWORD angelegt", AdminUsername);
            return;
        }

        var passwordFile = TryWritePasswordFile(app, password);
        AnnounceGeneratedPassword(app, password, passwordFile);
    }

    /// <summary>
    /// 24 Zeichen aus einem Alphabet ohne leicht verwechselbare Zeichen (0/O, 1/l/I),
    /// damit das Passwort abgetippt werden kann.
    /// </summary>
    private static string GeneratePassword()
    {
        const string alphabet = "abcdefghijkmnopqrstuvwxyzABCDEFGHJKLMNPQRSTUVWXYZ23456789";
        return new string(RandomNumberGenerator.GetItems<char>(alphabet, 24));
    }

    private static string? TryWritePasswordFile(WebApplication app, string password)
    {
        var path = Path.Combine(app.Environment.ContentRootPath, PasswordFileName);

        try
        {
            File.WriteAllText(path,
                $"Initiales Administrator-Passwort für '{AdminUsername}':{Environment.NewLine}" +
                $"{password}{Environment.NewLine}{Environment.NewLine}" +
                $"Diese Datei nach der ersten Anmeldung löschen.{Environment.NewLine}");

            LocalSettingsFile.RestrictToOwner(path);
            return path;
        }
        catch (Exception ex)
        {
            app.Logger.LogWarning(ex, "{File} konnte nicht geschrieben werden", PasswordFileName);
            return null;
        }
    }

    private static void AnnounceGeneratedPassword(WebApplication app, string password, string? passwordFile)
    {
        var location = passwordFile != null
            ? $"Auch gespeichert in: {passwordFile} (nach der ersten Anmeldung löschen)"
            : "Konnte nicht in eine Datei geschrieben werden — jetzt notieren!";

        app.Logger.LogWarning(
            """

            ======================================================================
             ERSTEINRICHTUNG — Administrator angelegt
             Benutzer:  {Username}
             Passwort:  {Password}

             {Location}
             Passwort nach der ersten Anmeldung unter "Sicherheit" ändern.
            ======================================================================
            """,
            AdminUsername, password, location);
    }
}
