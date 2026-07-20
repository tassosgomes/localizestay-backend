namespace LocalizeStay.Modules.Inventory.Domain.PropertyOnboardings;

/// <summary>
/// Persistent idempotency key recorded by <see cref="PropertyOnboarding"/> so that commands such as
/// <c>SubmitToCuration</c>, <c>SubmitDuplicateReview</c> and <c>RecordCurationReturn</c> remain safe
/// across process restarts. The in-memory <see cref="IdempotencyTracker"/> still guards the same
/// aggregate instance within a single unit of work; this entity makes the guarantee durable.
/// </summary>
internal sealed class IdempotencyKey
{
    internal Guid Id { get; private set; }
    internal Guid PropertyOnboardingId { get; private set; }
    internal Guid Key { get; private set; }
    internal IdempotencyScope Scope { get; private set; }
    internal string? PayloadFingerprint { get; private set; }
    internal DateTimeOffset CreatedAt { get; private set; }

    private IdempotencyKey()
    {
    }

    internal static IdempotencyKey Create(
        Guid propertyOnboardingId,
        Guid key,
        IdempotencyScope scope,
        DateTimeOffset createdAt,
        string? payloadFingerprint = null)
    {
        return new IdempotencyKey
        {
            Id = Guid.NewGuid(),
            PropertyOnboardingId = propertyOnboardingId,
            Key = key,
            Scope = scope,
            PayloadFingerprint = payloadFingerprint,
            CreatedAt = createdAt.ToUniversalTime(),
        };
    }
}
