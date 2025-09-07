using System.Collections.Concurrent;
using System.Text;
using Core.Logging.Constant;
using Core.Tracing.Factory;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;

namespace Core.Logging.Middleware;

public class LoggingMiddleware
{
    private readonly RequestDelegate _next;
    private readonly LoggingMiddlewareOptions? _options;

    /// <summary>
    /// Initializes a new instance of the LoggingMiddleware class.
    /// </summary>
    /// <param name="next">The next middleware in the pipeline.</param>
    public LoggingMiddleware(RequestDelegate next)
    {
        _next = next ?? throw new ArgumentNullException(nameof(next));
        _options = new LoggingMiddlewareOptions(); // Initialize with default options
    }

    /// <summary>
    /// Initializes a new instance of the LoggingMiddleware class with custom options.
    /// </summary>
    /// <param name="next">The next middleware in the pipeline.</param>
    /// <param name="options">Custom logging middleware options.</param>
    public LoggingMiddleware(RequestDelegate next, LoggingMiddlewareOptions options)
    {
        _next = next ?? throw new ArgumentNullException(nameof(next));
        _options = options ?? throw new ArgumentNullException(nameof(options));
    }

    ///<inheritdoc/>
    public async Task InvokeAsync(
        HttpContext httpContext,
        ICorrelationIdContextFactory correlationIdContextFactory,
        ILogger<LoggingMiddleware> logger
    )
    {
        var path = httpContext.Request.Path.Value;

        if (
            _options != null
            && _options.RoutesToBeExcluded.Any()
            && _options.RoutesToBeExcluded.Contains(path)
        )
        {
            await _next(httpContext);
        }
        else
        {
            var correlationId = GetOrCreateCorrelationId(httpContext, correlationIdContextFactory);

            await LogIncomingRequest(
                httpContext: httpContext,
                correlationId: correlationId,
                logger: logger
            );

            using (
                logger.BeginScope(
                    new Dictionary<string, object>() { { "CorrelationId", correlationId } }
                )
            )
            {
                await _next(httpContext);
            }

            await LogCompletedRequest(
                httpContext: httpContext,
                correlationId: correlationId,
                logger: logger
            );
        }
    }

    private async Task LogIncomingRequest(
        HttpContext httpContext,
        string correlationId,
        ILogger<LoggingMiddleware> logger
    )
    {
        var entry = await CreateHttpRequestLogEntry(httpContext, correlationId);

        using (logger.BeginScope(entry))
        {
            logger.LogInformation("Incoming Request");
        }
    }

    private async Task LogCompletedRequest(
        HttpContext httpContext,
        string correlationId,
        ILogger<LoggingMiddleware> logger
    )
    {
        var httpRequestLogEntry = await CreateHttpRequestLogEntry(httpContext, correlationId);
        var httpResponseLogEntry = await CreateHttpResponseLogEntry(
            httpContext,
            httpRequestLogEntry
        );

        var message = "CompletedRequest";
        var statusCode = httpContext.Response.StatusCode;

        using (logger.BeginScope(httpResponseLogEntry))
        {
            switch (statusCode)
            {
                case 200:
                    logger.LogInformation(message);
                    break;
                case 400:
                    logger.LogWarning(message);
                    break;
                case 401:
                case 403:
                case 500:
                    logger.LogError(message);
                    break;
                default:
                    logger.LogError(message);
                    break;
            }
        }
    }

    private async Task<ConcurrentDictionary<string, object>> CreateHttpRequestLogEntry(
        HttpContext context,
        string correlationId
    )
    {
        var result = new ConcurrentDictionary<string, object>();

        var request = context.Request;

        var hasBody = request.ContentLength > 0;
        var body = string.Empty;

        if (hasBody)
        {
            body = await GetRequestBody(request);
        }

        var query = request.Query.ToList();
        var forwardedHostname = request.Headers[LoggingConstant.ForwardedHostHeaderKey].ToString();
        var userAgent = request.Headers[LoggingConstant.UserAgentHeaderKey].ToString();

        result.TryAdd("HasHttpRequest", true);
        result.TryAdd("CorrelationId", correlationId ?? string.Empty);
        result.TryAdd("Method", request.Method.ToString());
        result.TryAdd("Url", request.Path);
        result.TryAdd("RequestBody", hasBody ? body : string.Empty);
        result.TryAdd(
            "Query",
            query.Count > 0
                ? query
                : new List<KeyValuePair<string, Microsoft.Extensions.Primitives.StringValues>>()
        );
        result.TryAdd("Scheme", request.Scheme);
        result.TryAdd("ContentType", request.ContentType ?? string.Empty);
        result.TryAdd("Protocol", request.Protocol);
        result.TryAdd(
            "Ip",
            context.Connection.RemoteIpAddress?.MapToIPv4().ToString() ?? "unknown"
        );
        result.TryAdd("Hostname", RemovePort(request.Host.ToString()) ?? "unknown");
        result.TryAdd(
            "ForwardedHostname",
            !string.IsNullOrEmpty(forwardedHostname) ? forwardedHostname : string.Empty
        );
        result.TryAdd("UserAgent", !string.IsNullOrEmpty(userAgent) ? userAgent : string.Empty);

        return result;
    }

    private async Task<ConcurrentDictionary<string, object>> CreateHttpResponseLogEntry(
        HttpContext context,
        ConcurrentDictionary<string, object> entries
    )
    {
        entries.TryAdd("HasHttpResponse", true);
        entries.TryAdd("StatusCode", context.Response.StatusCode);

        return entries;
    }

    private static string GetOrCreateCorrelationId(
        HttpContext context,
        ICorrelationIdContextFactory correlationIdContextFactory
    )
    {
        var hasCorrelationId = context.Request.Headers.TryGetValue(
            LoggingConstant.CorrelationIdHeaderKey,
            out var cid
        );
        var correlationId = hasCorrelationId ? cid.FirstOrDefault() : null;

        if (string.IsNullOrEmpty(correlationId))
        {
            correlationId = Guid.NewGuid().ToString();
            context.Request.Headers.Add(LoggingConstant.CorrelationIdHeaderKey, correlationId);
        }

        correlationIdContextFactory.Create(correlationId, LoggingConstant.CorrelationIdHeaderKey);

        return correlationId;
    }

    private static async Task<string> GetRequestBody(HttpRequest request)
    {
        // Create a new memory stream to copy the request body
        var buffer = new MemoryStream();
        await request.Body.CopyToAsync(buffer);
        buffer.Position = 0;

        // Read the body from the memory stream
        using (var reader = new StreamReader(buffer, Encoding.UTF8, leaveOpen: true))
        {
            var body = await reader.ReadToEndAsync();
            buffer.Position = 0; // Reset the position for the next middleware
            request.Body = buffer; // Replace the request body with the buffered stream
            return body;
        }
    }

    private static string? RemovePort(string host)
    {
        if (string.IsNullOrEmpty(host))
        {
            return null;
        }

        var hostComponents = host.Split(":");
        return hostComponents[0];
    }
}
