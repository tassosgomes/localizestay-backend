namespace LocalizeStay.SharedKernel.ErrorHandling;

public sealed class BusinessRuleViolationException : DomainException
{
    public BusinessRuleViolationException(string message) : base(message)
    {
    }

    public BusinessRuleViolationException(string message, string errorCode) : base(message)
    {
        ErrorCode = errorCode;
    }

    public override string ErrorCode { get; } = "business_rule_violation";
}
