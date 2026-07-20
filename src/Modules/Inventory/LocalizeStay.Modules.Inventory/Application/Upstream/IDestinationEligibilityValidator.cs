namespace LocalizeStay.Modules.Inventory.Application.Upstream;

internal interface IDestinationEligibilityValidator
{
    public Task EnsureApprovedAsync(string destinationId, CancellationToken cancellationToken);
}
