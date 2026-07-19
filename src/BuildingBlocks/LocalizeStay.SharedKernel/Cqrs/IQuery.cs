namespace LocalizeStay.SharedKernel.Cqrs;

/// <summary>Marker for a query: a side-effect-free read that returns <typeparamref name="TResponse"/>.</summary>
public interface IQuery<TResponse>
{
}
