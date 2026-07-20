using LocalizeStay.SharedKernel.ErrorHandling;

namespace LocalizeStay.Modules.Inventory.Domain.PropertyOnboardings;

internal sealed class IdempotencyTracker
{
    private readonly Dictionary<Guid, IdempotencyScope> _keys = new();

    internal void AssertAndRecord(Guid key, IdempotencyScope scope)
    {
        if (_keys.TryGetValue(key, out var existingScope))
        {
            if (existingScope != scope)
            {
                throw new ConflictException(
                    $"Idempotency key '{key}' was already used for a different operation.",
                    "IDEMPOTENCY_KEY_CONFLICT");
            }

            throw new IdempotentReplayException($"Idempotency key '{key}' was already processed.");
        }

        _keys[key] = scope;
    }
}
