namespace LocalizeStay.Modules.Inventory.Domain.PropertyOnboardings;

public sealed class DuplicateReview
{
    public Guid Id { get; private set; }
    public DuplicateReviewDecision Decision { get; private set; }
    public Guid? ExistingPropertyId { get; private set; }
    public string Justification { get; private set; } = string.Empty;
    public DateTimeOffset ReviewedAt { get; private set; }
    public string ReviewedBy { get; private set; } = string.Empty;
    public DateTimeOffset CreatedAt { get; private set; }

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
