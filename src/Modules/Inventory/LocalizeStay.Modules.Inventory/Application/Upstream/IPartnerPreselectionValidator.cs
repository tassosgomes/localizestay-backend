namespace LocalizeStay.Modules.Inventory.Application.Upstream;

internal interface IPartnerPreselectionValidator
{
    public Task EnsureEligibleAsync(string preselectionId, CancellationToken cancellationToken);
}
