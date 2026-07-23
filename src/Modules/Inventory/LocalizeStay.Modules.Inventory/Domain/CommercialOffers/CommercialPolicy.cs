using System.Collections.ObjectModel;
using LocalizeStay.Modules.Inventory.Application.LegalPolicies;
using LocalizeStay.SharedKernel.ErrorHandling;

namespace LocalizeStay.Modules.Inventory.Domain.CommercialOffers;

internal sealed class CommercialPolicy
{
    internal Guid Id { get; private set; }
    internal Guid PropertyId { get; private set; }
    internal PolicyType Type { get; private set; }
    internal PolicyStatus Status { get; private set; }
    internal bool IsDefault { get; private set; }
    internal int UsageCount { get; private set; }
    internal bool EverSubmitted { get; private set; }

    internal string? DeactivationReason { get; private set; }

    internal string Title { get; private set; } = string.Empty;
    internal string RulesSummary { get; private set; } = string.Empty;
    internal string RuleSetVersion { get; private set; } = string.Empty;

    internal DateTimeOffset CreatedAt { get; private set; }
    internal DateTimeOffset UpdatedAt { get; private set; }

    private readonly List<Guid> _submissionIds = [];

    internal IReadOnlyList<Guid> SubmissionIds => _submissionIds.AsReadOnly();

    private CommercialPolicy()
    {
    }

    internal static CommercialPolicy Create(
        Guid id,
        Guid propertyId,
        CommercialPolicyRuleSet ruleSet,
        bool isDefault,
        DateTimeOffset now)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(ruleSet.Title);
        ArgumentException.ThrowIfNullOrWhiteSpace(ruleSet.RulesSummary);
        ArgumentException.ThrowIfNullOrWhiteSpace(ruleSet.Version);

        if (ruleSet.Type is not (PolicyType.Flexible or PolicyType.NonRefundable))
            throw new ArgumentOutOfRangeException(nameof(ruleSet.Type));

        var utcNow = now.ToUniversalTime();

        return new CommercialPolicy
        {
            Id = id,
            PropertyId = propertyId,
            Type = ruleSet.Type,
            Title = ruleSet.Title.Trim(),
            RulesSummary = ruleSet.RulesSummary.Trim(),
            RuleSetVersion = ruleSet.Version.Trim(),
            Status = PolicyStatus.Active,
            IsDefault = isDefault,
            UsageCount = 0,
            EverSubmitted = false,
            CreatedAt = utcNow,
            UpdatedAt = utcNow,
        };
    }

    internal void SetDefault()
    {
        if (Status != PolicyStatus.Active)
            throw new BusinessRuleViolationException(
                "Only active policies can be set as default.",
                "POLICY_NOT_ACTIVE");

        IsDefault = true;
    }

    internal void UnsetDefault()
    {
        IsDefault = false;
    }

    internal void Deactivate(string? deactivationReason = null)
    {
        if (Status != PolicyStatus.Active)
            throw new BusinessRuleViolationException(
                "Policy is already inactive.",
                "POLICY_ALREADY_INACTIVE");

        Status = PolicyStatus.Inactive;
        IsDefault = false;
        DeactivationReason = deactivationReason;
    }

    internal void MarkSubmitted(Guid submissionId)
    {
        EverSubmitted = true;
        _submissionIds.Add(submissionId);
    }

    internal void IncrementUsage()
    {
        UsageCount++;
    }

    internal void DecrementUsage()
    {
        if (UsageCount <= 0)
            throw new BusinessRuleViolationException(
                "Policy usage count is already zero.",
                "POLICY_USAGE_UNDERFLOW");

        UsageCount--;
    }

    internal bool CanDelete()
    {
        return !EverSubmitted && !IsDefault && UsageCount == 0;
    }

    internal void UpdateTimestamp(DateTimeOffset now)
    {
        UpdatedAt = now.ToUniversalTime();
    }
}
