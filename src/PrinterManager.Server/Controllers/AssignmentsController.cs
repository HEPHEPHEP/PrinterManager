using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PrinterManager.Server.Services;
using PrinterManager.Shared.DTOs;

namespace PrinterManager.Server.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class AssignmentsController : ControllerBase
{
    private readonly IAssignmentService _assignmentService;

    public AssignmentsController(IAssignmentService assignmentService)
    {
        _assignmentService = assignmentService;
    }

    [HttpGet]
    public async Task<ActionResult<List<AssignmentDto>>> GetAll()
    {
        var assignments = await _assignmentService.GetAllAssignmentsAsync();
        return Ok(assignments);
    }

    [HttpGet("user/{userId}")]
    public async Task<ActionResult<List<AssignmentDto>>> GetForUser(int userId)
    {
        var assignments = await _assignmentService.GetAssignmentsForUserAsync(userId);
        return Ok(assignments);
    }

    [HttpGet("client/{clientId}")]
    public async Task<ActionResult<List<AssignmentDto>>> GetForClient(int clientId)
    {
        var assignments = await _assignmentService.GetAssignmentsForClientAsync(clientId);
        return Ok(assignments);
    }

    [HttpPost]
    [Authorize(Roles = "Administrator")]
    public async Task<ActionResult<AssignmentDto>> Create([FromBody] CreateAssignmentDto dto)
    {
        try
        {
            var assignment = await _assignmentService.CreateAssignmentAsync(dto);
            return CreatedAtAction(nameof(GetAll), new { id = assignment.Id }, assignment);
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpPost("bulk")]
    [Authorize(Roles = "Administrator")]
    public async Task<ActionResult<List<AssignmentDto>>> CreateBulk([FromBody] BulkAssignmentDto dto)
    {
        try
        {
            var assignments = await _assignmentService.CreateBulkAssignmentsAsync(dto);
            return Ok(assignments);
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpDelete("{id}")]
    [Authorize(Roles = "Administrator")]
    public async Task<ActionResult> Delete(int id)
    {
        var result = await _assignmentService.DeleteAssignmentAsync(id);
        if (!result)
            return NotFound();

        return NoContent();
    }

    [HttpPut("{id}/set-default")]
    [Authorize(Roles = "Administrator")]
    public async Task<ActionResult<AssignmentDto>> SetDefault(int id)
    {
        try
        {
            var assignment = await _assignmentService.SetDefaultPrinterAsync(id);
            if (assignment == null)
                return NotFound();

            return Ok(assignment);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }
}
