using System.Diagnostics;
using LocalizeStay.Modules.Inventory.Application.Upstream;
using LocalizeStay.SharedKernel.ErrorHandling;
using Microsoft.Extensions.Options;

namespace LocalizeStay.Modules.Inventory.Infrastructure.Upstream;

internal sealed class UpstreamEligibilityOptions
{
    internal const string SectionName = "Inventory:UpstreamEligibility";

    public string Mode { get; set; } = "Pilot";

    public List<string> EligiblePreselectionIds { get; set; } = [];

    public List<string> ApprovedDestinationIds { get; set; } = [];
}

internal static class InventoryUpstreamActivitySource
{
    internal const string Name = "LocalizeStay.Inventory.Upstream";

    internal static readonly ActivitySource Instance = new(Name);
}

internal sealed class ConfiguredPartnerPreselectionValidator(IOptions<UpstreamEligibilityOptions> options)
    : IPartnerPreselectionValidator
{
    public Task EnsureEligibleAsync(string preselectionId, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(preselectionId);
        cancellationToken.ThrowIfCancellationRequested();

        using var activity = InventoryUpstreamActivitySource.Instance.StartActivity("inventory.upstream.validate_preselection");
        activity?.SetTag("inventory.upstream.validator", "partner-preselection");
        activity?.SetTag("inventory.upstream.eligible", options.Value.EligiblePreselectionIds.Contains(preselectionId, StringComparer.Ordinal));

        if (!options.Value.EligiblePreselectionIds.Contains(preselectionId, StringComparer.Ordinal))
        {
            throw new BusinessRuleViolationException(
                "Partner is not eligible for onboarding.",
                "PARTNER_NOT_PRESELECTED");
        }

        return Task.CompletedTask;
    }
}

internal sealed class ConfiguredDestinationEligibilityValidator(IOptions<UpstreamEligibilityOptions> options)
    : IDestinationEligibilityValidator
{
    public Task EnsureApprovedAsync(string destinationId, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(destinationId);
        cancellationToken.ThrowIfCancellationRequested();

        using var activity = InventoryUpstreamActivitySource.Instance.StartActivity("inventory.upstream.validate_destination");
        activity?.SetTag("inventory.upstream.validator", "destination-eligibility");
        activity?.SetTag("inventory.upstream.approved", options.Value.ApprovedDestinationIds.Contains(destinationId, StringComparer.Ordinal));

        if (!options.Value.ApprovedDestinationIds.Contains(destinationId, StringComparer.Ordinal))
        {
            throw new BusinessRuleViolationException(
                "Destination is not approved for onboarding.",
                "DESTINATION_NOT_APPROVED");
        }

        return Task.CompletedTask;
    }
}
