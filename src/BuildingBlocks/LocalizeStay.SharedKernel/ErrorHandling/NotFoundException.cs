namespace LocalizeStay.SharedKernel.ErrorHandling;

public sealed class NotFoundException(string message) : DomainException(message)
{
    public override string ErrorCode => "resource_not_found";
}
