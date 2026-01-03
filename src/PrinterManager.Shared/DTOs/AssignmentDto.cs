namespace PrinterManager.Shared.DTOs;

public class CreateAssignmentDto
{
    public int PrinterId { get; set; }
    public string AssignmentType { get; set; } = "User"; // "User" or "Client"
    public int? UserId { get; set; }
    public int? ClientId { get; set; }
    public bool IsDefaultPrinter { get; set; }
}

public class BulkAssignmentDto
{
    public List<int> PrinterIds { get; set; } = new();
    public string AssignmentType { get; set; } = "User"; // "User" or "Client"
    public List<int> UserIds { get; set; } = new();
    public List<int> ClientIds { get; set; } = new();
    public bool IsDefaultPrinter { get; set; }
}

public class AssignmentDto
{
    public int Id { get; set; }
    public int PrinterId { get; set; }
    public string PrinterName { get; set; } = string.Empty;
    public string? ServiceNumber { get; set; }
    public string AssignmentType { get; set; } = string.Empty;
    public int? UserId { get; set; }
    public string? UserPrincipalName { get; set; }
    public int? ClientId { get; set; }
    public string? ClientHostname { get; set; }
    public bool IsDefaultPrinter { get; set; }
}
