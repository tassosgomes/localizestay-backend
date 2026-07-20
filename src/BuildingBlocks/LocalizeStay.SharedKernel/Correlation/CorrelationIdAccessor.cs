namespace LocalizeStay.SharedKernel.Correlation;

/// <summary>
/// <see cref="AsyncLocal{T}"/>-backed correlation id holder. Safe to register as a singleton: state
/// flows with the logical call context, not with the instance.
/// </summary>
public sealed class CorrelationIdAccessor : ICorrelationIdAccessor
{
    private static readonly AsyncLocal<string?> _current = new();

    public string CorrelationId => _current.Value ??= Guid.NewGuid().ToString("n");

    public void Set(string correlationId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(correlationId);
        _current.Value = correlationId;
    }
}
