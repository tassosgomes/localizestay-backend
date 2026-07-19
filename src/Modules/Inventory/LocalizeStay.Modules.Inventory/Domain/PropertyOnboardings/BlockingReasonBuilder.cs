using System.Collections.ObjectModel;

namespace LocalizeStay.Modules.Inventory.Domain.PropertyOnboardings;

internal static class BlockingReasonBuilder
{
    public static IReadOnlyList<BlockingReason> Build(
        OnboardingLifecycleStatus lifecycleStatus,
        IEnumerable<ReadinessGate> gates,
        IEnumerable<PendingIssue> issues,
        bool duplicateReviewRequiresDecision)
    {
        var reasons = new List<BlockingReason>();

        if (lifecycleStatus == OnboardingLifecycleStatus.Closed)
        {
            reasons.Add(new BlockingReason(
                BlockingReasonCode.OnboardingClosed,
                "Onboarding is closed.",
                null));
            return reasons;
        }

        if (lifecycleStatus == OnboardingLifecycleStatus.SubmittedToCuration)
        {
            reasons.Add(new BlockingReason(
                BlockingReasonCode.AlreadySubmitted,
                "Onboarding has already been submitted to curation.",
                null));
            return reasons;
        }

        foreach (var gate in gates.Where(g => g.Status != ReadinessGateStatus.Validated))
        {
            reasons.Add(new BlockingReason(
                BlockingReasonCode.GateNotValidated,
                $"Gate '{gate.Type}' is not validated.",
                gate.Type.ToString()));
        }

        foreach (var issue in issues.Where(i => i.Status == PendingIssueStatus.Open))
        {
            reasons.Add(new BlockingReason(
                BlockingReasonCode.PendingIssueOpen,
                $"Pending issue '{issue.Description}' is open.",
                issue.Id.ToString()));
        }

        if (duplicateReviewRequiresDecision)
        {
            reasons.Add(new BlockingReason(
                BlockingReasonCode.DuplicateReviewRequired,
                "Duplicate review decision is required.",
                null));
        }

        return reasons.AsReadOnly();
    }
}
