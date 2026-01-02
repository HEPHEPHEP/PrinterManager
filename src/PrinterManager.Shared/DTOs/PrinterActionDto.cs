namespace PrinterManager.Shared.DTOs;

public enum PrinterAction
{
    Install,
    Remove,
    SetDefault
}

public class PrinterActionDto
{
    public int PrinterId { get; set; }
    public PrinterAction Action { get; set; }
    public string? SharePath { get; set; }
    public string? PrinterName { get; set; }
}

public class PrinterActionsResponse
{
    public List<PrinterActionDto> Actions { get; set; } = new();
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;
}
