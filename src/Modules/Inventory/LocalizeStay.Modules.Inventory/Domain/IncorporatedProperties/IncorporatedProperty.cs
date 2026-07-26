using LocalizeStay.SharedKernel.ErrorHandling;

namespace LocalizeStay.Modules.Inventory.Domain.IncorporatedProperties;

internal sealed class IncorporatedProperty
{
    internal Guid Id { get; private set; }
    internal Guid PartnerId { get; private set; }
    internal string PropertyName { get; private set; } = string.Empty;
    internal string DestinationId { get; private set; } = string.Empty;
    internal string InitialActor { get; private set; } = string.Empty;
    internal Guid OnboardingId { get; private set; }
    internal DateTimeOffset CreatedAt { get; private set; }
    internal DateTimeOffset UpdatedAt { get; private set; }

    private IncorporatedProperty()
    {
    }

    internal static IncorporatedProperty Create(
        Guid id,
        Guid partnerId,
        string propertyName,
        string destinationId,
        string initialActor,
        DateTimeOffset now)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(propertyName);
        ArgumentException.ThrowIfNullOrWhiteSpace(destinationId);
        ArgumentException.ThrowIfNullOrWhiteSpace(initialActor);

        if (propertyName.Length is < 2 or > 180)
        {
            throw new ArgumentException("PropertyName must be between 2 and 180 characters.", nameof(propertyName));
        }

        if (destinationId.Length > 120)
        {
            throw new ArgumentException("DestinationId must be at most 120 characters.", nameof(destinationId));
        }

        if (initialActor.Length > 200)
        {
            throw new ArgumentException("InitialActor must be at most 200 characters.", nameof(initialActor));
        }

        var utcNow = now.ToUniversalTime();

        return new IncorporatedProperty
        {
            Id = id,
            PartnerId = partnerId,
            PropertyName = propertyName.Trim(),
            DestinationId = destinationId.Trim(),
            InitialActor = initialActor.Trim(),
            OnboardingId = id,
            CreatedAt = utcNow,
            UpdatedAt = utcNow,
        };
    }

    internal void Sync(string propertyName, string destinationId, DateTimeOffset updatedAt)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(propertyName);
        ArgumentException.ThrowIfNullOrWhiteSpace(destinationId);

        if (propertyName.Length is < 2 or > 180)
        {
            throw new ArgumentException("PropertyName must be between 2 and 180 characters.", nameof(propertyName));
        }

        if (destinationId.Length > 120)
        {
            throw new ArgumentException("DestinationId must be at most 120 characters.", nameof(destinationId));
        }

        var utcUpdatedAt = updatedAt.ToUniversalTime();

        if (utcUpdatedAt < UpdatedAt)
        {
            throw new BusinessRuleViolationException(
                "Cannot sync IncorporatedProperty with an older timestamp.",
                "INCORPORATED_PROPERTY_STALE_SYNC");
        }

        PropertyName = propertyName.Trim();
        DestinationId = destinationId.Trim();
        UpdatedAt = utcUpdatedAt;
    }
}
