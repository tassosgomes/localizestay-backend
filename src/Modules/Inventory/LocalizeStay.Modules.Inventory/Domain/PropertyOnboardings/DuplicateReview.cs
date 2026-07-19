namespace LocalizeStay.Modules.Inventory.Domain.PropertyOnboardings;

internal sealed class DuplicateReview
{
    internal Guid Id { get; private set; }
    internal DuplicateReviewDecision Decision { get; private set; }
    internal Guid? ExistingPropertyId { get; private set; }
    internal string Justification { get; private set; } = string.Empty;
    internal DateTimeOffset ReviewedAt { get; private set; }
    internal string ReviewedBy { get; private set; } = string.Empty;
    internal DateTimeOffset CreatedAt { get; private set; }

    private DuplicateReview()
    {
    }

    internal static DuplicateReview Create(
        Guid id,
        DuplicateReviewDecision decision,
        Guid? existingPropertyId,
        string justification,
        DateTimeOffset reviewedAt,
        string reviewedBy)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(justification);
        ArgumentException.ThrowIfNullOrWhiteSpace(reviewedBy);

        if (justification.Length is < 10 or > 1000)
        {
            throw new ArgumentException("Justification must be between 10 and 1000 characters.", nameof(justification));
        }

        if (decision == DuplicateReviewDecision.DuplicateOfExistingProperty && !existingPropertyId.HasValue)
        {
            throw new ArgumentException(
                "ExistingPropertyId is required when decision is duplicateOfExistingProperty.",
                nameof(existingPropertyId));
        }

        return new DuplicateReview
        {
            Id = id,
            Decision = decision,
            ExistingPropertyId = existingPropertyId,
            Justification = justification.Trim(),
            ReviewedAt = reviewedAt.ToUniversalTime(),
            ReviewedBy = reviewedBy.Trim(),
            CreatedAt = reviewedAt.ToUniversalTime(),
        };
    }
}
