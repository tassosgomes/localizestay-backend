namespace LocalizeStay.SharedKernel.Cqrs;

/// <summary>
/// Native CQRS dispatcher (no MediatR, per ADR-0001): resolves the registered handler for a command
/// or query and invokes it. Modules depend on this abstraction instead of calling handlers directly.
/// </summary>
public interface IDispatcher
{
    Task<TResponse> SendAsync<TResponse>(ICommand<TResponse> command, CancellationToken cancellationToken = default);

    Task<TResponse> QueryAsync<TResponse>(IQuery<TResponse> query, CancellationToken cancellationToken = default);
}
