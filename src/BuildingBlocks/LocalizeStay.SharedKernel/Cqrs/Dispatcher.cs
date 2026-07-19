using System.Reflection;

namespace LocalizeStay.SharedKernel.Cqrs;

/// <summary>
/// Default <see cref="IDispatcher"/> implementation. Resolves <c>ICommandHandler&lt;,&gt;</c> or
/// <c>IQueryHandler&lt;,&gt;</c> for the request's runtime type via the DI container and invokes it
/// through reflection — no MediatR, no <c>dynamic</c>, per ADR-0001.
/// </summary>
public sealed class Dispatcher : IDispatcher
{
    private readonly IServiceProvider _serviceProvider;

    public Dispatcher(IServiceProvider serviceProvider)
    {
        _serviceProvider = serviceProvider ?? throw new ArgumentNullException(nameof(serviceProvider));
    }

    public Task<TResponse> SendAsync<TResponse>(ICommand<TResponse> command, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);
        return InvokeHandlerAsync<TResponse>(typeof(ICommandHandler<,>), command, cancellationToken);
    }

    public Task<TResponse> QueryAsync<TResponse>(IQuery<TResponse> query, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(query);
        return InvokeHandlerAsync<TResponse>(typeof(IQueryHandler<,>), query, cancellationToken);
    }

    private Task<TResponse> InvokeHandlerAsync<TResponse>(Type openHandlerType, object request, CancellationToken cancellationToken)
    {
        var handlerType = openHandlerType.MakeGenericType(request.GetType(), typeof(TResponse));
        var handler = _serviceProvider.GetService(handlerType)
            ?? throw new InvalidOperationException(
                $"No handler of type '{handlerType.Name}' is registered for request '{request.GetType().Name}'.");

        var handleMethod = handlerType.GetMethod("HandleAsync", BindingFlags.Public | BindingFlags.Instance)
            ?? throw new InvalidOperationException($"Handler type '{handlerType.Name}' does not expose a HandleAsync method.");

        return (Task<TResponse>)handleMethod.Invoke(handler, [request, cancellationToken])!;
    }
}
