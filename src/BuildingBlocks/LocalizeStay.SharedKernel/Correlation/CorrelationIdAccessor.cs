namespace LocalizeStay.SharedKernel.Correlation;

/// <summary>
/// <see cref="AsyncLocal{T}"/>-backed correlation id holder. Safe to register as a singleton: state
/// flows with the logical call context, not with the instance.
/// </summary>
public sealed class CorrelationIdAccessor : ICorrelationIdAccessor
{
    private static readonly AsyncLocal<string?> Current = new();

    public string CorrelationId => Current.Value ??= Guid.NewGuid().ToString("n");

    public void Set(string correlationId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(correlationId);
        Current.Value = correlationId;
    }
}
