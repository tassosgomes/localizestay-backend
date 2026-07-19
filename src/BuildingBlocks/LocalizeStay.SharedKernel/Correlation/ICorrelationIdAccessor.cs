namespace LocalizeStay.SharedKernel.Correlation;

/// <summary>Exposes the correlation id for the current logical operation (HTTP request, background job, or event handling).</summary>
public interface ICorrelationIdAccessor
{
    string CorrelationId { get; }
}
