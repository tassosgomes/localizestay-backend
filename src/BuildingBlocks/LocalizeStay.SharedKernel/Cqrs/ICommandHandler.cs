namespace LocalizeStay.SharedKernel.Cqrs;

/// <summary>Handles a single <typeparamref name="TCommand"/> and produces its <typeparamref name="TResponse"/>.</summary>
public interface ICommandHandler<in TCommand, TResponse> where TCommand : ICommand<TResponse>
{
    public Task<TResponse> HandleAsync(TCommand command, CancellationToken cancellationToken);
}
