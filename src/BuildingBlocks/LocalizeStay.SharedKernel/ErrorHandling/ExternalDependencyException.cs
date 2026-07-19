namespace LocalizeStay.SharedKernel.ErrorHandling;

/// <summary>
/// Raised by an anti-corruption layer when an external provider (payment, WhatsApp, transactional
/// communication) is unavailable or returns an unexpected result. Provider-specific details stay
/// behind the adapter; only this stable shape crosses into the domain (architecture baseline:
/// Anti-corruption layers).
/// </summary>
public sealed class ExternalDependencyException : DomainException
{
    public ExternalDependencyException(string message) : base(message)
    {
    }

    public ExternalDependencyException(string message, Exception innerException) : base(message, innerException)
    {
    }

    public override string ErrorCode => "external_dependency_unavailable";
}
