namespace Core.Models;

public class CoreException
{
    public required int Status { get; set; }
    public required string Message { get; set; }
    public required string Detail { get; set; } = string.Empty;
    public string StackTrace { get; set; } = string.Empty;
    public string InnerException { get; set; } = string.Empty;
    public string Source { get; set; } = string.Empty;
}
