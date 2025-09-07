namespace Core.Tracing;

public class CorrelationIdContext
{
    /// <summary>
    /// Creates a new instance of the <see cref="Vittoria.EmissioneDiretta.Commons.CorrelationIdContext"/> class.
    /// </summary>
    /// <param name="correlationId">The CorrelationId set in the context.</param>
    /// <param name="headerKey">The key used to store the CorrelationId in the current request.</param>
    public CorrelationIdContext(string correlationId, string headerKey)
    {
        CorrelationId = correlationId;
        HeaderKey = headerKey;
    }

    /// <summary>
    /// Gets the name of the header containing the CorrelationId for the current request.
    /// </summary>
    public string HeaderKey { get; }

    /// <summary>
    /// Gets the CorrelationId for the current request.
    /// </summary>
    public string CorrelationId { get; }
}
