namespace LocalizeStay.SharedKernel.Time;

/// <summary>Abstraction over the current instant, so modules never call <see cref="DateTimeOffset.UtcNow"/> directly and stay testable.</summary>
public interface IClock
{
    public DateTimeOffset UtcNow { get; }
}
