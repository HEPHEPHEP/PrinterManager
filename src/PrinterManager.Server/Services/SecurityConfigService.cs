using Microsoft.EntityFrameworkCore;
using PrinterManager.Server.Data;
using PrinterManager.Shared.DTOs;
using PrinterManager.Shared.Models;

namespace PrinterManager.Server.Services;

public interface ISecurityConfigService
{
    Task<LdapConfigDto> GetLdapConfigAsync();
    Task<LdapConfigDto> UpdateLdapConfigAsync(LdapConfigDto dto);
    Task<SslConfigDto> GetSslConfigAsync();
    Task<SslConfigDto> UpdateSslConfigAsync(SslConfigDto dto);
}

public class SecurityConfigService : ISecurityConfigService
{
    private readonly PrinterManagerDbContext _context;

    public SecurityConfigService(PrinterManagerDbContext context)
    {
        _context = context;
    }

    public async Task<LdapConfigDto> GetLdapConfigAsync()
    {
        return ToDto(await GetOrCreateLdapConfigAsync());
    }

    public async Task<LdapConfigDto> UpdateLdapConfigAsync(LdapConfigDto dto)
    {
        if (dto.Port is < 1 or > 65535)
            throw new ArgumentException("Der LDAP-Port muss zwischen 1 und 65535 liegen.");

        if (dto.Enabled)
        {
            if (string.IsNullOrWhiteSpace(dto.Server))
                throw new ArgumentException("LDAP-Server ist erforderlich.");
            if (string.IsNullOrWhiteSpace(dto.BaseDn))
                throw new ArgumentException("Base-DN ist erforderlich.");
            if (string.IsNullOrWhiteSpace(dto.UserDnTemplate) || !dto.UserDnTemplate.Contains("{0}"))
                throw new ArgumentException("Die User-DN-Vorlage muss den Platzhalter {0} enthalten.");
        }

        var config = await GetOrCreateLdapConfigAsync();
        config.Enabled = dto.Enabled;
        config.Server = dto.Server;
        config.Port = dto.Port;
        config.BaseDn = dto.BaseDn;
        config.UserDnTemplate = dto.UserDnTemplate;
        config.LastModified = DateTime.UtcNow;

        await _context.SaveChangesAsync();

        return ToDto(config);
    }

    public async Task<SslConfigDto> GetSslConfigAsync()
    {
        return ToDto(await GetOrCreateSslConfigAsync());
    }

    public async Task<SslConfigDto> UpdateSslConfigAsync(SslConfigDto dto)
    {
        if (dto.HttpsPort is < 1 or > 65535)
            throw new ArgumentException("Der HTTPS-Port muss zwischen 1 und 65535 liegen.");

        var config = await GetOrCreateSslConfigAsync();
        config.Enabled = dto.Enabled;
        config.HttpsPort = dto.HttpsPort;

        if (!string.IsNullOrEmpty(dto.CertificatePath))
            config.CertificatePath = dto.CertificatePath;

        if (!string.IsNullOrEmpty(dto.CertificatePassword))
            config.CertificatePassword = dto.CertificatePassword;

        config.LastModified = DateTime.UtcNow;

        await _context.SaveChangesAsync();

        return ToDto(config);
    }

    private async Task<LdapConfiguration> GetOrCreateLdapConfigAsync()
    {
        var config = await _context.LdapConfigurations.FirstOrDefaultAsync();
        if (config != null)
            return config;

        config = new LdapConfiguration { Id = 1 };
        _context.LdapConfigurations.Add(config);
        await _context.SaveChangesAsync();
        return config;
    }

    private async Task<SslConfiguration> GetOrCreateSslConfigAsync()
    {
        var config = await _context.SslConfigurations.FirstOrDefaultAsync();
        if (config != null)
            return config;

        config = new SslConfiguration { Id = 1 };
        _context.SslConfigurations.Add(config);
        await _context.SaveChangesAsync();
        return config;
    }

    private static LdapConfigDto ToDto(LdapConfiguration config) => new()
    {
        Enabled = config.Enabled,
        Server = config.Server,
        Port = config.Port,
        BaseDn = config.BaseDn,
        UserDnTemplate = config.UserDnTemplate
    };

    // CertificatePassword wird bewusst NICHT zurückgegeben.
    private static SslConfigDto ToDto(SslConfiguration config) => new()
    {
        Enabled = config.Enabled,
        HttpsPort = config.HttpsPort,
        CertificatePath = config.CertificatePath
    };
}
