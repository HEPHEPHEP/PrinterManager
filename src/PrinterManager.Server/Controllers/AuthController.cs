using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PrinterManager.Server.Services;
using PrinterManager.Shared.DTOs;
using System.Security.Claims;

namespace PrinterManager.Server.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class AuthController : ControllerBase
{
    private readonly IAuthenticationService _authService;

    public AuthController(IAuthenticationService authService)
    {
        _authService = authService;
    }

    [HttpPost("login")]
    [AllowAnonymous]
    public async Task<ActionResult<LoginResponseDto>> Login([FromBody] LoginDto dto)
    {
        var result = await _authService.LoginAsync(dto);
        if (!result.Success)
        {
            return Unauthorized(result);
        }

        return Ok(result);
    }

    [HttpPost("register")]
    [Authorize(Roles = "Administrator")]
    public async Task<ActionResult<UserDto>> Register([FromBody] RegisterUserDto dto)
    {
        try
        {
            var user = await _authService.RegisterUserAsync(dto);
            return Ok(user);
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpGet("users")]
    [Authorize(Roles = "Administrator")]
    public async Task<ActionResult<List<UserDto>>> GetUsers()
    {
        var users = await _authService.GetAllUsersAsync();
        return Ok(users);
    }

    [HttpDelete("users/{id}")]
    [Authorize(Roles = "Administrator")]
    public async Task<ActionResult> DeleteUser(int id)
    {
        if (id == CurrentUserId)
            return BadRequest(new { message = "Der eigene Account kann nicht gelöscht werden." });

        try
        {
            var result = await _authService.DeleteUserAsync(id);
            if (!result)
                return NotFound();

            return NoContent();
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpPut("users/{id}")]
    [Authorize(Roles = "Administrator")]
    public async Task<ActionResult<UserDto>> UpdateUser(int id, [FromBody] RegisterUserDto dto)
    {
        try
        {
            var user = await _authService.UpdateUserAsync(id, dto);
            if (user == null)
                return NotFound();

            return Ok(user);
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpPut("users/{id}/role")]
    [Authorize(Roles = "Administrator")]
    public async Task<ActionResult<UserDto>> UpdateUserRole(int id, [FromBody] UpdateUserRoleDto dto)
    {
        try
        {
            var user = await _authService.UpdateUserRoleAsync(id, dto);
            if (user == null)
                return NotFound();

            return Ok(user);
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    private int? CurrentUserId =>
        int.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var id) ? id : null;
}
