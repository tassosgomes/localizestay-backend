namespace LocalizeStay.SharedKernel.Cqrs;

/// <summary>
/// Marker for a command: an operation that mutates state and returns <typeparamref name="TResponse"/>.
/// Use <see cref="Unit"/> as the response type for commands that return no meaningful data.
/// </summary>
public interface ICommand<TResponse>
{
}
