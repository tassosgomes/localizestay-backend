namespace LocalizeStay.SharedKernel.ErrorHandling;

public sealed class BusinessRuleViolationException : DomainException
{
    public BusinessRuleViolationException(string message) : base(message)
    {
        Metadata = new Dictionary<string, object?>(StringComparer.Ordinal);
    }

    public BusinessRuleViolationException(string message, string errorCode) : base(message)
    {
        ErrorCode = errorCode;
        Metadata = new Dictionary<string, object?>(StringComparer.Ordinal);
    }

    public BusinessRuleViolationException(
        string message,
        string errorCode,
        IReadOnlyDictionary<string, object?> metadata) : base(message)
    {
        ErrorCode = errorCode;
        Metadata = metadata ?? new Dictionary<string, object?>(StringComparer.Ordinal);
    }

    public override string ErrorCode { get; } = "business_rule_violation";

    /// <summary>
    /// Optional structured metadata surfaced in the Problem Details <c>metadata</c> extension.
    /// Must contain only non-sensitive identifiers, dates and quantities.
    /// </summary>
    public IReadOnlyDictionary<string, object?> Metadata { get; }
}
