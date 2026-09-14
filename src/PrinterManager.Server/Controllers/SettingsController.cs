using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PrinterManager.Server.Services;
using PrinterManager.Shared.DTOs;

namespace PrinterManager.Server.Controllers;

/// <summary>
/// Server-Einstellungen aus <c>appsettings.Local.json</c>. Ausschließlich für
/// Administratoren — hierüber lässt sich unter anderem die Client-Authentifizierung
/// abschalten.
/// </summary>
[ApiController]
[Route("api/[controller]")]
[Authorize(Roles = "Administrator")]
public class SettingsController : ControllerBase
{
    private readonly IServerSettingsService _settings;

    public SettingsController(IServerSettingsService settings)
    {
        _settings = settings;
    }

    [HttpGet]
    public ActionResult<ServerSettingsDto> Get() => Ok(_settings.Get());

    [HttpGet("status")]
    public ActionResult<ServerStatusDto> GetStatus() => Ok(_settings.GetStatus());

    /// <summary>
    /// Liefert den gemeinsamen Client-Schlüssel im Klartext, damit er bei den Clients
    /// hinterlegt werden kann. Bewusst ein eigener Aufruf, damit er nicht bei jedem
    /// Laden der Einstellungen mitgeht.
    /// </summary>
    [HttpGet("client-key")]
    public ActionResult<ClientApiKeyDto> GetClientApiKey() =>
        Ok(new ClientApiKeyDto { Key = _settings.GetClientApiKey() });

    [HttpPut]
    public ActionResult<SaveSettingsResultDto> Update([FromBody] ServerSettingsDto dto)
    {
        try
        {
            return Ok(_settings.Save(dto));
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
        catch (InvalidOperationException ex)
        {
            return StatusCode(StatusCodes.Status500InternalServerError, new { message = ex.Message });
        }
    }
}
