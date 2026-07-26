using LocalizeStay.Modules.Inventory.Application.LegalPolicies;
using Microsoft.Extensions.Options;

namespace LocalizeStay.Modules.Inventory.Infrastructure.LegalPolicies;

internal sealed class LegalPolicyOptions
{
    internal const string SectionName = "Inventory:LegalPolicies";

    public List<LegalPolicyRuleSetEntry> RuleSets { get; set; } = [];
}

internal sealed class LegalPolicyRuleSetEntry
{
    public string Type { get; set; } = string.Empty;

    public string Title { get; set; } = string.Empty;

    public string RulesSummary { get; set; } = string.Empty;

    public string RuleSetVersion { get; set; } = string.Empty;
}

internal static class LegalPolicyOptionsValidator
{
    internal static bool Validate(LegalPolicyOptions options)
    {
        if (options.RuleSets.Count != 2)
        {
            return false;
        }

        var flexible = options.RuleSets.SingleOrDefault(
            r => string.Equals(r.Type, "flexible", StringComparison.OrdinalIgnoreCase));

        var nonRefundable = options.RuleSets.SingleOrDefault(
            r => string.Equals(r.Type, "nonRefundable", StringComparison.OrdinalIgnoreCase));

        if (flexible is null || nonRefundable is null)
        {
            return false;
        }

        return RuleSetEntryIsValid(flexible) && RuleSetEntryIsValid(nonRefundable);
    }

    private static bool RuleSetEntryIsValid(LegalPolicyRuleSetEntry entry)
        => !string.IsNullOrWhiteSpace(entry.Title)
            && !string.IsNullOrWhiteSpace(entry.RulesSummary)
            && !string.IsNullOrWhiteSpace(entry.RuleSetVersion);
}

internal sealed class ConfiguredLegalPolicyCatalog(IOptions<LegalPolicyOptions> options) : ILegalPolicyCatalog
{
    private readonly IReadOnlyDictionary<PolicyType, CommercialPolicyRuleSet> _catalog = BuildCatalog(options.Value);

    public CommercialPolicyRuleSet GetCurrent(PolicyType policyType)
        => _catalog[policyType];

    private static IReadOnlyDictionary<PolicyType, CommercialPolicyRuleSet> BuildCatalog(LegalPolicyOptions options)
    {
        var flexible = options.RuleSets.Single(
            r => string.Equals(r.Type, "flexible", StringComparison.OrdinalIgnoreCase));

        var nonRefundable = options.RuleSets.Single(
            r => string.Equals(r.Type, "nonRefundable", StringComparison.OrdinalIgnoreCase));

        return new Dictionary<PolicyType, CommercialPolicyRuleSet>
        {
            [PolicyType.Flexible] = new(
                PolicyType.Flexible,
                flexible.Title,
                flexible.RulesSummary,
                flexible.RuleSetVersion),
            [PolicyType.NonRefundable] = new(
                PolicyType.NonRefundable,
                nonRefundable.Title,
                nonRefundable.RulesSummary,
                nonRefundable.RuleSetVersion),
        };
    }
}
