using AwesomeAssertions;
using LocalizeStay.Modules.Inventory.Application.PropertyOnboardings;
using LocalizeStay.Modules.Inventory.Application.Validation;
using LocalizeStay.Modules.Inventory.Domain.PropertyOnboardings;
using LocalizeStay.Modules.Inventory.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace LocalizeStay.UnitTests.Inventory;

public sealed class PropertyOnboardingMetricsQueryHandlerTests
{
    [Fact]
    public async Task HandleAsync_UsesExclusiveToBoundaryAndDestinationFilterWithoutTracking()
    {
        await using var dbContext = CreateDbContext();
        var from = DateTimeOffset.Parse("2026-07-01T00:00:00-03:00");
        var to = DateTimeOffset.Parse("2026-07-02T00:00:00-03:00");
        await dbContext.PropertyOnboardings.AddRangeAsync(
            CreateOnboarding("recife-pe", from),
            CreateOnboarding("recife-pe", to),
            CreateOnboarding("salvador-ba", from.AddHours(2)));
        await dbContext.SaveChangesAsync();
        dbContext.ChangeTracker.Clear();
        var handler = new GetPropertyOnboardingMetricsQueryHandler(dbContext, new GetPropertyOnboardingMetricsQueryValidator());

        var response = await handler.HandleAsync(new GetPropertyOnboardingMetricsQuery(from, to, "recife-pe"), CancellationToken.None);

        response.TotalOpened.Should().Be(1);
        response.PropertiesPreparedForCuration.Should().Be(0);
        response.SubmittedWithinTenBusinessDays.Should().Be(new PercentageMetric(0, 0, 0m));
        response.CurationReturnRate.Should().Be(new PercentageMetric(0, 0, 0m));
        response.CommunicationsWithinFourBusinessHours.Should().Be(new PercentageMetric(0, 0, 0m));
        dbContext.ChangeTracker.Entries().Should().BeEmpty();
    }

    [Fact]
    public async Task HandleAsync_NormalizesTimezoneOffsetsForTheRequestedInterval()
    {
        await using var dbContext = CreateDbContext();
        var from = DateTimeOffset.Parse("2026-07-01T00:00:00-03:00");
        var to = DateTimeOffset.Parse("2026-07-01T04:00:00Z");
        await dbContext.PropertyOnboardings.AddAsync(CreateOnboarding("recife-pe", DateTimeOffset.Parse("2026-07-01T00:30:00-03:00")));
        await dbContext.SaveChangesAsync();
        var handler = new GetPropertyOnboardingMetricsQueryHandler(dbContext, new GetPropertyOnboardingMetricsQueryValidator());

        var response = await handler.HandleAsync(new GetPropertyOnboardingMetricsQuery(from, to, null), CancellationToken.None);

        response.TotalOpened.Should().Be(1);
    }

    [Fact]
    public async Task HandleAsync_ReturnsAllMetricNumeratorsDenominatorsAndPercentagesForDeterministicDataset()
    {
        await using var dbContext = CreateDbContext();
        var openedAt = DateTimeOffset.Parse("2026-07-01T09:00:00Z");
        var timelySubmission = CreateReadyOnboarding("recife-pe", openedAt);
        timelySubmission.SubmitToCuration(Guid.NewGuid(), "Ready for curation.", openedAt.AddDays(5), "staff-001");
        timelySubmission.RecordCommunication(Guid.NewGuid(), CommunicationChannel.Email, openedAt, openedAt.AddHours(2), "Processed within SLA.", true, "staff-001", openedAt.AddHours(2));
        var returnedSubmission = CreateReadyOnboarding("recife-pe", openedAt.AddHours(1));
        returnedSubmission.SubmitToCuration(Guid.NewGuid(), "Ready for curation.", openedAt.AddDays(12), "staff-001");
        returnedSubmission.RecordCurationReturn(Guid.NewGuid(), "curation-001", CurationReturnReasonCode.MissingData, "Missing property document.", [new CurationReturnIssue("Missing property document.", PendingOwnerType.Staff, ReadinessGateType.PropertyBasics)], openedAt.AddDays(13), "staff-002", Guid.NewGuid());
        returnedSubmission.RecordCommunication(Guid.NewGuid(), CommunicationChannel.Whatsapp, openedAt, openedAt.AddHours(5), "Processed outside SLA.", false, "staff-002", openedAt.AddHours(5));
        var pendingOnboarding = CreateOnboarding("recife-pe", openedAt.AddHours(2));
        pendingOnboarding.RecordCommunication(Guid.NewGuid(), CommunicationChannel.Email, openedAt, openedAt.AddHours(3), "Processed within SLA.", true, "staff-003", openedAt.AddHours(3));
        await dbContext.PropertyOnboardings.AddRangeAsync(timelySubmission, returnedSubmission, pendingOnboarding, CreateOnboarding("salvador-ba", openedAt));
        await dbContext.SaveChangesAsync();
        var handler = new GetPropertyOnboardingMetricsQueryHandler(dbContext, new GetPropertyOnboardingMetricsQueryValidator());

        var response = await handler.HandleAsync(new GetPropertyOnboardingMetricsQuery(openedAt.AddHours(-1), openedAt.AddDays(1), "recife-pe"), CancellationToken.None);

        response.TotalOpened.Should().Be(3);
        response.PropertiesPreparedForCuration.Should().Be(2);
        response.SubmittedWithinTenBusinessDays.Should().Be(new PercentageMetric(1, 2, 50m));
        response.CurationReturnRate.Should().Be(new PercentageMetric(1, 2, 50m));
        response.CompleteGatesSubmissionRate.Should().Be(new PercentageMetric(2, 2, 100m));
        response.CommunicationsWithinFourBusinessHours.Should().Be(new PercentageMetric(2, 3, 66.67m));
        response.ByLifecycleStatus.Should().BeEquivalentTo([
            new LifecycleStatusCountResponse("inProgress", 1),
            new LifecycleStatusCountResponse("submittedToCuration", 1),
            new LifecycleStatusCountResponse("returnedByCuration", 1),
        ]);
    }

    private static PropertyOnboarding CreateOnboarding(string destinationId, DateTimeOffset openedAt) =>
        PropertyOnboarding.Create(Guid.NewGuid(), Guid.NewGuid(), "preselection", new Property("Test property", destinationId, new Address("Street", "1", null, "District", "City", "PE", "50000-000", "BR")), openedAt, TimeSpan.FromDays(10));

    private static PropertyOnboarding CreateReadyOnboarding(string destinationId, DateTimeOffset openedAt)
    {
        var onboarding = CreateOnboarding(destinationId, openedAt);
        foreach (var gateType in Enum.GetValues<ReadinessGateType>())
        {
            var evidenceKind = gateType switch
            {
                ReadinessGateType.SignedContract => EvidenceKind.Contract,
                ReadinessGateType.AuthorizedContact => EvidenceKind.FormalAuthorization,
                ReadinessGateType.OperationalChannel => EvidenceKind.Communication,
                _ => EvidenceKind.OfficialDocument,
            };
            var evidence = new EvidenceReference(evidenceKind, $"reference-{gateType}", "Verified evidence.");
            if (gateType == ReadinessGateType.SignedContract)
            {
                onboarding.ValidateGate(gateType, [evidence], new ContractReference("contracts/001", "CON-001", openedAt, ["Test partner"]), "staff-001", openedAt);
                continue;
            }
            onboarding.ValidateGate(gateType, [evidence], "staff-001", openedAt);
        }
        return onboarding;
    }

    private static InventoryDbContext CreateDbContext() => new(new DbContextOptionsBuilder<InventoryDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);
}
