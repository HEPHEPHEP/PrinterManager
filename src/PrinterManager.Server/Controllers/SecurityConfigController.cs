using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PrinterManager.Server.Services;
using PrinterManager.Shared.DTOs;

namespace PrinterManager.Server.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize(Roles = "Administrator")]
public class SecurityConfigController : ControllerBase
{
    private readonly ISecurityConfigService _securityConfigService;

    public SecurityConfigController(ISecurityConfigService securityConfigService)
    {
        _securityConfigService = securityConfigService;
    }

    [HttpGet("ldap")]
    public async Task<ActionResult<LdapConfigDto>> GetLdapConfig()
    {
        var config = await _securityConfigService.GetLdapConfigAsync();
        return Ok(config);
    }

    [HttpPut("ldap")]
    public async Task<ActionResult<LdapConfigDto>> UpdateLdapConfig([FromBody] LdapConfigDto dto)
    {
        try
        {
            var config = await _securityConfigService.UpdateLdapConfigAsync(dto);
            return Ok(config);
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }
}
