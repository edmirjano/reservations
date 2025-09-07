using Core.Tracing.ContextAccessor;

namespace Core.Tracing.Factory;

public class CorrelationIdContextFactory : ICorrelationIdContextFactory
{
    private readonly ICorrelationContextAccessor _correlationContextAccessor;

    /// <summary>
    /// Creates a new instance of the <see cref="Vittoria.Infrastructure.CorrelationId.CorrelationIdContextAccessor"/> class.
    /// </summary>
    /// <param name="correlationContextAccessor">An instance of the Context Accessor used to set the Context for the current request.</param>
    public CorrelationIdContextFactory(ICorrelationContextAccessor correlationContextAccessor)
    {
        _correlationContextAccessor = correlationContextAccessor;
    }

    /// <inheritdoc />
    public CorrelationIdContext Create(string correlationId, string headerKey)
    {
        var context = new CorrelationIdContext(correlationId, headerKey);

        if (_correlationContextAccessor is not null)
        {
            _correlationContextAccessor.Context = context;
        }

        return context;
    }
}
