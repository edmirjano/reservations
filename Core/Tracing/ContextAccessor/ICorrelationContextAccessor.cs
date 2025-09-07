namespace Core.Tracing.ContextAccessor;

public interface ICorrelationContextAccessor
{
    /// <summary>
    /// The context of the current request.
    /// </summary>
    CorrelationIdContext Context { get; set; }
}
