using System.Collections.ObjectModel;

namespace LocalizeStay.Modules.Inventory.Domain.CommercialOffers;

internal sealed record CompletenessResult(
    bool IsComplete,
    IReadOnlyList<PendingIssueType> PendingIssues,
    int AccommodationCount,
    int BlockingIssueCount)
{
    internal static CompletenessResult Incomplete(params PendingIssueType[] issues) =>
        new(false, new ReadOnlyCollection<PendingIssueType>(issues), 0, issues.Length);

    internal static CompletenessResult Incomplete(IReadOnlyList<PendingIssueType> issues, int accommodationCount) =>
        new(false, issues, accommodationCount, issues.Count);

    internal static CompletenessResult Complete(int accommodationCount) =>
        new(true, Array.Empty<PendingIssueType>(), accommodationCount, 0);
}

internal static class CommercialOfferCompleteness
{
    internal static CompletenessResult Compute(
        int accommodationCount,
        int completeAccommodationCount,
        int activeRateCount,
        bool hasAnyRateOverlap)
    {
        if (accommodationCount == 0)
        {
            return CompletenessResult.Incomplete(
                PendingIssueType.MissingPolicy,
                PendingIssueType.IncompleteAccommodation,
                PendingIssueType.MissingActiveRate);
        }

        var issues = new List<PendingIssueType>();

        if (completeAccommodationCount == 0)
            issues.Add(PendingIssueType.IncompleteAccommodation);

        if (activeRateCount == 0)
            issues.Add(PendingIssueType.MissingActiveRate);

        if (hasAnyRateOverlap)
            issues.Add(PendingIssueType.RatePeriodOverlap);

        if (issues.Count > 0)
            return CompletenessResult.Incomplete(issues, accommodationCount);

        return CompletenessResult.Complete(accommodationCount);
    }
}
