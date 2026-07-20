namespace LocalizeStay.SharedKernel.Cqrs;

/// <summary>Handles a single <typeparamref name="TQuery"/> and produces its <typeparamref name="TResponse"/>.</summary>
public interface IQueryHandler<in TQuery, TResponse> where TQuery : IQuery<TResponse>
{
    public Task<TResponse> HandleAsync(TQuery query, CancellationToken cancellationToken);
}
