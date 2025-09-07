namespace Core.Tracing.Factory;

public interface ICorrelationIdContextFactory
{
    /// <summary>
    /// Creates a new instance of the <see cref="Vittoria.Infrastructure.CorrelationId.CorrelationIdContext"/> with the CorrelationId set for the current request.
    /// </summary>
    /// <param name="correlationId">The CorrelationId set in the context.</param>
    /// <param name="headerKey">The key used to store the CorrelationId in the current request.</param>
    /// <returns>A new instance of the CorrelationIdContext.</returns>
    CorrelationIdContext Create(string correlationId, string headerKey);
}
