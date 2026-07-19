namespace LocalizeStay.SharedKernel.ErrorHandling;

public sealed class ConflictException(string message) : DomainException(message)
{
    public override string ErrorCode => "resource_conflict";
}
