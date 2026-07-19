namespace LocalizeStay.SharedKernel.ErrorHandling;

public sealed class BusinessRuleViolationException(string message) : DomainException(message)
{
    public override string ErrorCode => "business_rule_violation";
}
