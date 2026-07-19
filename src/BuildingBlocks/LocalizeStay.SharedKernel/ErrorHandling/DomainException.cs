namespace LocalizeStay.SharedKernel.ErrorHandling;

/// <summary>
/// Base for exceptions that represent an expected, named domain-level failure — as opposed to an
/// unexpected infrastructure fault. The global exception handler maps these to stable Problem
/// Details responses without leaking internals (architecture baseline: Erros e versionamento).
/// </summary>
public abstract class DomainException : Exception
{
    protected DomainException(string message) : base(message)
    {
    }

    protected DomainException(string message, Exception innerException) : base(message, innerException)
    {
    }

    /// <summary>Stable, machine-readable error code surfaced in the Problem Details response.</summary>
    public abstract string ErrorCode { get; }
}
