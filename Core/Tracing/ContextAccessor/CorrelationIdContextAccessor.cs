namespace Core.Tracing.ContextAccessor;

/// <inheritdoc />
public class CorrelationIdContextAccessor : ICorrelationContextAccessor
{
    private static readonly AsyncLocal<CorrelationIdContext?> _context =
        new AsyncLocal<CorrelationIdContext?>();

    /// <inheritdoc />
    public CorrelationIdContext? Context
    {
        get => _context.Value;
        set => _context.Value = value;
    }
}
