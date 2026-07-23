using AwesomeAssertions;
using LocalizeStay.Modules.Inventory.Application.LegalPolicies;
using LocalizeStay.Modules.Inventory.Infrastructure.LegalPolicies;
using Microsoft.Extensions.Options;
using Moq;

namespace LocalizeStay.UnitTests.Inventory;

public sealed class LegalPolicyCatalogTests
{
    private static LegalPolicyOptions ValidOptions => new()
    {
        RuleSets =
        [
            new LegalPolicyRuleSetEntry
            {
                Type = "flexible",
                Title = "Flexível",
                RulesSummary = "Cancelamento gratuito até sete dias antes do check-in.",
                RuleSetVersion = "BR-2026-01",
            },
            new LegalPolicyRuleSetEntry
            {
                Type = "nonRefundable",
                Title = "Não-Reembolsável",
                RulesSummary = "Sem reembolso para cancelamento voluntário, ressalvadas hipóteses legais.",
                RuleSetVersion = "BR-2026-01",
            },
        ],
    };

    [Fact]
    public void GetCurrent_WithFlexibleType_ReturnsImmutableRuleSet()
    {
        var options = Options.Create(ValidOptions);
        var catalog = new ConfiguredLegalPolicyCatalog(options);

        var result = catalog.GetCurrent(PolicyType.Flexible);

        result.Type.Should().Be(PolicyType.Flexible);
        result.Title.Should().Be("Flexível");
        result.RulesSummary.Should().Be("Cancelamento gratuito até sete dias antes do check-in.");
        result.Version.Should().Be("BR-2026-01");
    }

    [Fact]
    public void GetCurrent_WithNonRefundableType_ReturnsImmutableRuleSet()
    {
        var options = Options.Create(ValidOptions);
        var catalog = new ConfiguredLegalPolicyCatalog(options);

        var result = catalog.GetCurrent(PolicyType.NonRefundable);

        result.Type.Should().Be(PolicyType.NonRefundable);
        result.Title.Should().Be("Não-Reembolsável");
        result.RulesSummary.Should().Be("Sem reembolso para cancelamento voluntário, ressalvadas hipóteses legais.");
        result.Version.Should().Be("BR-2026-01");
    }

    [Fact]
    public void GetCurrent_MultipleCalls_ReturnsSameReference()
    {
        var options = Options.Create(ValidOptions);
        var catalog = new ConfiguredLegalPolicyCatalog(options);

        var first = catalog.GetCurrent(PolicyType.Flexible);
        var second = catalog.GetCurrent(PolicyType.Flexible);

        first.Should().BeSameAs(second);
    }

    [Fact]
    public void Validate_WithMissingTitle_Fails()
    {
        var invalid = ValidOptions;
        invalid.RuleSets[0].Title = string.Empty;

        LegalPolicyOptionsValidator.Validate(invalid).Should().BeFalse();
    }

    [Fact]
    public void Validate_WithMissingRulesSummary_Fails()
    {
        var invalid = ValidOptions;
        invalid.RuleSets[0].RulesSummary = "   ";

        LegalPolicyOptionsValidator.Validate(invalid).Should().BeFalse();
    }

    [Fact]
    public void Validate_WithMissingVersion_Fails()
    {
        var invalid = ValidOptions;
        invalid.RuleSets[0].RuleSetVersion = null!;

        LegalPolicyOptionsValidator.Validate(invalid).Should().BeFalse();
    }

    [Fact]
    public void Validate_WithOnlyFlexibleType_Fails()
    {
        var invalid = new LegalPolicyOptions
        {
            RuleSets =
            [
                new LegalPolicyRuleSetEntry
                {
                    Type = "flexible",
                    Title = "Flex",
                    RulesSummary = "Summary",
                    RuleSetVersion = "v1",
                },
            ],
        };

        LegalPolicyOptionsValidator.Validate(invalid).Should().BeFalse();
    }

    [Fact]
    public void Validate_WithUnknownType_Fails()
    {
        var invalid = new LegalPolicyOptions
        {
            RuleSets =
            [
                new LegalPolicyRuleSetEntry
                {
                    Type = "flexible",
                    Title = "Flex",
                    RulesSummary = "Summary",
                    RuleSetVersion = "v1",
                },
                new LegalPolicyRuleSetEntry
                {
                    Type = "custom",
                    Title = "Custom",
                    RulesSummary = "Custom policy",
                    RuleSetVersion = "v1",
                },
            ],
        };

        LegalPolicyOptionsValidator.Validate(invalid).Should().BeFalse();
    }

    [Fact]
    public void Validate_WithThreeEntries_Fails()
    {
        var invalid = new LegalPolicyOptions
        {
            RuleSets =
            [
                new LegalPolicyRuleSetEntry
                {
                    Type = "flexible",
                    Title = "Flex",
                    RulesSummary = "Summary",
                    RuleSetVersion = "v1",
                },
                new LegalPolicyRuleSetEntry
                {
                    Type = "nonRefundable",
                    Title = "NonRef",
                    RulesSummary = "Summary",
                    RuleSetVersion = "v1",
                },
                new LegalPolicyRuleSetEntry
                {
                    Type = "flexible",
                    Title = "Flex 2",
                    RulesSummary = "Summary 2",
                    RuleSetVersion = "v2",
                },
            ],
        };

        LegalPolicyOptionsValidator.Validate(invalid).Should().BeFalse();
    }

    [Fact]
    public void Validate_WithBothValidEntryTypes_Succeeds()
    {
        LegalPolicyOptionsValidator.Validate(ValidOptions).Should().BeTrue();
    }

    [Fact]
    public void Validate_WithNullTitle_Fails()
    {
        var invalid = ValidOptions;
        invalid.RuleSets[0].Title = null!;

        LegalPolicyOptionsValidator.Validate(invalid).Should().BeFalse();
    }
}
