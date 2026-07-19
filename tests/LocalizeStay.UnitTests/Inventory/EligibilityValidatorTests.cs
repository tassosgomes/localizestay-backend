using AwesomeAssertions;
using LocalizeStay.Modules.Inventory;
using LocalizeStay.Modules.Inventory.Application.Upstream;
using LocalizeStay.Modules.Inventory.Infrastructure.Upstream;
using LocalizeStay.SharedKernel.ErrorHandling;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace LocalizeStay.UnitTests.Inventory;

public class EligibilityValidatorTests
{
    private static readonly UpstreamEligibilityOptions _options = new()
    {
        Mode = "Pilot",
        EligiblePreselectionIds = ["preselection-001"],
        ApprovedDestinationIds = ["recife-pe"],
    };

    [Fact]
    public async Task EnsureEligibleAsync_WithConfiguredPreselection_ShouldSucceed()
    {
        IPartnerPreselectionValidator sut = new ConfiguredPartnerPreselectionValidator(Options.Create(_options));

        var act = () => sut.EnsureEligibleAsync("preselection-001", CancellationToken.None);

        await act.Should().NotThrowAsync();
    }

    [Fact]
    public async Task EnsureEligibleAsync_WithUnknownPreselection_ShouldThrowContractCode()
    {
        IPartnerPreselectionValidator sut = new ConfiguredPartnerPreselectionValidator(Options.Create(_options));

        var act = () => sut.EnsureEligibleAsync("preselection-unknown", CancellationToken.None);

        var exception = await act.Should().ThrowAsync<BusinessRuleViolationException>();
        exception.Which.ErrorCode.Should().Be("PARTNER_NOT_PRESELECTED");
    }

    [Fact]
    public async Task EnsureApprovedAsync_WithConfiguredDestination_ShouldSucceed()
    {
        IDestinationEligibilityValidator sut = new ConfiguredDestinationEligibilityValidator(Options.Create(_options));

        var act = () => sut.EnsureApprovedAsync("recife-pe", CancellationToken.None);

        await act.Should().NotThrowAsync();
    }

    [Fact]
    public async Task EnsureApprovedAsync_WithUnknownDestination_ShouldThrowContractCode()
    {
        IDestinationEligibilityValidator sut = new ConfiguredDestinationEligibilityValidator(Options.Create(_options));

        var act = () => sut.EnsureApprovedAsync("salvador-ba", CancellationToken.None);

        var exception = await act.Should().ThrowAsync<BusinessRuleViolationException>();
        exception.Which.ErrorCode.Should().Be("DESTINATION_NOT_APPROVED");
    }

    [Fact]
    public async Task EnsureEligibleAsync_WithCancelledToken_ShouldPropagateCancellation()
    {
        IPartnerPreselectionValidator sut = new ConfiguredPartnerPreselectionValidator(Options.Create(_options));
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();

        var act = () => sut.EnsureEligibleAsync("preselection-001", cancellation.Token);

        await act.Should().ThrowAsync<OperationCanceledException>();
    }

    [Fact]
    public void ResolveOptions_WithMissingEligibilityConfiguration_ShouldFailFast()
    {
        using var provider = CreateProvider(new Dictionary<string, string?>());

        var act = () => provider.GetRequiredService<IOptions<UpstreamEligibilityOptions>>().Value;

        act.Should().Throw<OptionsValidationException>()
            .WithMessage("*upstream eligibility must use Pilot mode*");
    }

    [Fact]
    public void ResolveOptions_WithInconsistentEligibilityConfiguration_ShouldFailFast()
    {
        using var provider = CreateProvider(new Dictionary<string, string?>
        {
            ["Inventory:UpstreamEligibility:Mode"] = "Http",
            ["Inventory:UpstreamEligibility:EligiblePreselectionIds:0"] = "preselection-001",
            ["Inventory:UpstreamEligibility:ApprovedDestinationIds:0"] = "recife-pe",
        });

        var act = () => provider.GetRequiredService<IOptions<UpstreamEligibilityOptions>>().Value;

        act.Should().Throw<OptionsValidationException>()
            .WithMessage("*upstream eligibility must use Pilot mode*");
    }

    private static ServiceProvider CreateProvider(IReadOnlyDictionary<string, string?> values)
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(values)
            .Build();
        var services = new ServiceCollection();
        new InventoryModule().RegisterServices(services, configuration);
        return services.BuildServiceProvider();
    }
}
