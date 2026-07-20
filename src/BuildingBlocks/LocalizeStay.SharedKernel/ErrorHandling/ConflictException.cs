namespace LocalizeStay.SharedKernel.ErrorHandling;

public sealed class ConflictException : DomainException
{
    public ConflictException(string message) : base(message)
    {
    }

    public ConflictException(string message, string errorCode) : base(message)
    {
        ErrorCode = errorCode;
    }

    public ConflictException(string message, string errorCode, Guid conflictingResourceId) : base(message)
    {
        ErrorCode = errorCode;
        ConflictingResourceId = conflictingResourceId;
    }

    /// <summary>
    /// Stable, machine-readable error code surfaced in the Problem Details response. Defaults to
    /// <c>resource_conflict</c>; specific conflict cases (e.g. <c>DUPLICATE_LEGAL_IDENTIFIER</c>)
    /// override it via the constructor overload.
    /// </summary>
    public override string ErrorCode { get; } = "resource_conflict";

    /// <summary>Optional id of the resource that already owns the conflicting value (e.g. the partner that already holds the legal identifier).</summary>
    public Guid? ConflictingResourceId { get; }
}
