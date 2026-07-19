namespace LocalizeStay.Modules.Inventory.Domain.PropertyOnboardings;

internal static class OnboardingEntityLocator
{
    internal static ReadinessGate FindGate(this IEnumerable<ReadinessGate> gates, ReadinessGateType gateType)
    {
        var gate = gates.SingleOrDefault(g => g.Type == gateType);
        return gate ?? throw new ArgumentException($"Gate '{gateType}' not found.", nameof(gateType));
    }

    internal static PendingIssue FindPendingIssue(this IEnumerable<PendingIssue> issues, Guid issueId)
    {
        var issue = issues.SingleOrDefault(i => i.Id == issueId);
        return issue ?? throw new ArgumentException($"Pending issue '{issueId}' not found.", nameof(issueId));
    }
}
