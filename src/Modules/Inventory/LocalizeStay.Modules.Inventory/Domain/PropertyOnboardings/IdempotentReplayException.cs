namespace LocalizeStay.Modules.Inventory.Domain.PropertyOnboardings;

public sealed class IdempotentReplayException : Exception
{
    public IdempotentReplayException(string message) : base(message)
    {
    }
}
