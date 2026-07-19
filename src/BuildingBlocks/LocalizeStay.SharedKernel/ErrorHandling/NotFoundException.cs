namespace LocalizeStay.SharedKernel.ErrorHandling;

public sealed class NotFoundException : DomainException
{
    public NotFoundException(string message) : base(message)
    {
    }

    public NotFoundException(string message, string errorCode) : base(message)
    {
        ErrorCode = errorCode;
    }

    public override string ErrorCode { get; } = "resource_not_found";
}
