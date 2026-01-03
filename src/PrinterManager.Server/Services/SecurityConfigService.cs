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
    private readonly IConfiguration _configuration;

    public SecurityConfigService(PrinterManagerDbContext context, IConfiguration configuration)
    {
        _context = context;
        _configuration = configuration;
    }

    public async Task<LdapConfigDto> GetLdapConfigAsync()
    {
        var config = await _context.LdapConfigurations.FirstAsync();
        return new LdapConfigDto
        {
            Enabled = config.Enabled,
            Server = config.Server,
            Port = config.Port,
            BaseDn = config.BaseDn,
            UserDnTemplate = config.UserDnTemplate
        };
    }

    public async Task<LdapConfigDto> UpdateLdapConfigAsync(LdapConfigDto dto)
    {
        var config = await _context.LdapConfigurations.FirstAsync();
        config.Enabled = dto.Enabled;
        config.Server = dto.Server;
        config.Port = dto.Port;
        config.BaseDn = dto.BaseDn;
        config.UserDnTemplate = dto.UserDnTemplate;
        config.LastModified = DateTime.UtcNow;

        await _context.SaveChangesAsync();

        return new LdapConfigDto
        {
            Enabled = config.Enabled,
            Server = config.Server,
            Port = config.Port,
            BaseDn = config.BaseDn,
            UserDnTemplate = config.UserDnTemplate
        };
    }

    public async Task<SslConfigDto> GetSslConfigAsync()
    {
        var config = await _context.SslConfigurations.FirstAsync();
        return new SslConfigDto
        {
            Enabled = config.Enabled,
            HttpsPort = config.HttpsPort,
            CertificatePath = config.CertificatePath
        };
    }

    public async Task<SslConfigDto> UpdateSslConfigAsync(SslConfigDto dto)
    {
        var config = await _context.SslConfigurations.FirstAsync();
        config.Enabled = dto.Enabled;
        config.HttpsPort = dto.HttpsPort;

        if (!string.IsNullOrEmpty(dto.CertificatePath))
            config.CertificatePath = dto.CertificatePath;

        if (!string.IsNullOrEmpty(dto.CertificatePassword))
            config.CertificatePassword = dto.CertificatePassword;

        config.LastModified = DateTime.UtcNow;

        await _context.SaveChangesAsync();

        return new SslConfigDto
        {
            Enabled = config.Enabled,
            HttpsPort = config.HttpsPort,
            CertificatePath = config.CertificatePath
        };
    }
}
