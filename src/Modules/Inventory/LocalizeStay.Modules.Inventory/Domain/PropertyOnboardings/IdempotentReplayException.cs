namespace LocalizeStay.Modules.Inventory.Domain.PropertyOnboardings;

internal sealed class IdempotentReplayException : Exception
{
    internal IdempotentReplayException(string message) : base(message)
    {
    }
}
