namespace LocalizeStay.Modules.Inventory.Application.LegalPolicies;

internal interface ILegalPolicyCatalog
{
    public CommercialPolicyRuleSet GetCurrent(PolicyType policyType);
}

internal enum PolicyType
{
    Flexible,
    NonRefundable,
}

internal sealed record CommercialPolicyRuleSet(
    PolicyType Type,
    string Title,
    string RulesSummary,
    string Version);
