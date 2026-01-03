using Microsoft.AspNetCore.Mvc;
using PrinterManager.Server.Services;
using PrinterManager.Shared.DTOs;

namespace PrinterManager.Server.Controllers;

[ApiController]
[Route("api/[controller]")]
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
    public async Task<ActionResult<AssignmentDto>> Create([FromBody] CreateAssignmentDto dto)
    {
        var assignment = await _assignmentService.CreateAssignmentAsync(dto);
        return CreatedAtAction(nameof(GetAll), new { id = assignment.Id }, assignment);
    }

    [HttpPost("bulk")]
    public async Task<ActionResult<List<AssignmentDto>>> CreateBulk([FromBody] BulkAssignmentDto dto)
    {
        var assignments = await _assignmentService.CreateBulkAssignmentsAsync(dto);
        return Ok(assignments);
    }

    [HttpDelete("{id}")]
    public async Task<ActionResult> Delete(int id)
    {
        var result = await _assignmentService.DeleteAssignmentAsync(id);
        if (!result)
            return NotFound();

        return NoContent();
    }
}
