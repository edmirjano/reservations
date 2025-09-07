namespace Core.Logging.Middleware;

public class LoggingMiddlewareOptions
{
    /// <summary>
    /// Indicates the routes that should not be logged.
    /// </summary>
    public List<string> RoutesToBeExcluded { get; set; } = new List<string>();
}
