using System.Diagnostics;
using System.Diagnostics.Metrics;
using AwesomeAssertions;
using LocalizeStay.Modules.Inventory.Application.PropertyOnboardings;
using LocalizeStay.Modules.Inventory.Application.Validation;
using LocalizeStay.Modules.Inventory.Domain.PropertyOnboardings;
using LocalizeStay.Modules.Inventory.Infrastructure;
using LocalizeStay.SharedKernel.Auditing;
using LocalizeStay.SharedKernel.Correlation;
using LocalizeStay.SharedKernel.Time;
using Microsoft.EntityFrameworkCore;
using Moq;

namespace LocalizeStay.UnitTests.Inventory;

public sealed class SubmissionCommandHandlerTests
{
    [Fact]
    public void LifecycleTelemetry_WhenEmitted_ShouldCaptureSubmitSpanAndAllCounters()
    {
        var capturedActivities = new List<Activity>();
        var capturedMeasurements = new Dictionary<string, long>();
        using var activityListener = new ActivityListener
        {
            ShouldListenTo = source => source.Name == "LocalizeStay.Inventory.Lifecycle",
            Sample = static (ref ActivityCreationOptions<ActivityContext> _) => ActivitySamplingResult.AllData,
            ActivityStopped = activity => capturedActivities.Add(activity)
        };
        using var meterListener = new MeterListener();
        meterListener.InstrumentPublished = (instrument, listener) =>
        {
            if (instrument.Meter.Name == "LocalizeStay.Inventory.Lifecycle")
            {
                listener.EnableMeasurementEvents(instrument);
            }
        };
        meterListener.SetMeasurementEventCallback<long>((instrument, measurement, _, _) =>
        {
            capturedMeasurements[instrument.Name] = capturedMeasurements.GetValueOrDefault(instrument.Name) + measurement;
        });
        ActivitySource.AddActivityListener(activityListener);
        meterListener.Start();

        using (InventoryLifecycleTelemetry.ActivitySource.StartActivity("inventory.onboarding.submit")) { }
        InventoryLifecycleTelemetry.SubmitSuccess.Add(1);
        InventoryLifecycleTelemetry.SubmitBlocked.Add(1);
        InventoryLifecycleTelemetry.OutboxFailure.Add(1);

        capturedActivities.Should().ContainSingle(activity => activity.OperationName == "inventory.onboarding.submit");
        capturedMeasurements.Should().Contain(new KeyValuePair<string, long>("inventory.onboarding.submit.success", 1));
        capturedMeasurements.Should().Contain(new KeyValuePair<string, long>("inventory.onboarding.submit.blocked", 1));
        capturedMeasurements.Should().Contain(new KeyValuePair<string, long>("inventory.onboarding.outbox.failure", 1));
    }

    [Fact]
    public async Task HandleAsync_WithReadyOnboarding_ShouldPersistStateAndOutboxOnce()
    {
        await using var dbContext = new InventoryDbContext(new DbContextOptionsBuilder<InventoryDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);
        var onboarding = ReadyOnboarding();
        await dbContext.PropertyOnboardings.AddAsync(onboarding);
        await dbContext.SaveChangesAsync();
        var handler = new SubmitToCurationCommandHandler(dbContext, Mock.Of<IBusinessAuditWriter>(), new FixedClock(), Mock.Of<ICorrelationIdAccessor>(item => item.CorrelationId == "corr-001"), new SubmitToCurationCommandValidator());
        var key = Guid.NewGuid();

        var result = await handler.HandleAsync(new SubmitToCurationCommand(onboarding.Id, key, "All evidence was reviewed.", "staff-001"), CancellationToken.None);
        var replay = await handler.HandleAsync(new SubmitToCurationCommand(onboarding.Id, key, "All evidence was reviewed.", "staff-001"), CancellationToken.None);

        result.Onboarding.LifecycleStatus.Should().Be("submittedToCuration");
        replay.IntegrationEvent.Id.Should().Be(result.IntegrationEvent.Id);
        (await dbContext.OutboxMessages.CountAsync()).Should().Be(1);
        (await dbContext.IdempotencyKeys.CountAsync()).Should().Be(1);
    }

    [Fact]
    public async Task HandleAsync_WithSameKeyAndDifferentPayload_ShouldReturnStateConflict()
    {
        await using var dbContext = new InventoryDbContext(new DbContextOptionsBuilder<InventoryDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);
        var onboarding = ReadyOnboarding();
        await dbContext.PropertyOnboardings.AddAsync(onboarding);
        await dbContext.SaveChangesAsync();
        var handler = new SubmitToCurationCommandHandler(dbContext, Mock.Of<IBusinessAuditWriter>(), new FixedClock(), Mock.Of<ICorrelationIdAccessor>(item => item.CorrelationId == "corr-001"), new SubmitToCurationCommandValidator());
        var key = Guid.NewGuid();
        await handler.HandleAsync(new SubmitToCurationCommand(onboarding.Id, key, "All evidence was reviewed.", "staff-001"), CancellationToken.None);

        var action = () => handler.HandleAsync(new SubmitToCurationCommand(onboarding.Id, key, "A different decision note.", "staff-001"), CancellationToken.None);

        var exception = await action.Should().ThrowAsync<LocalizeStay.SharedKernel.ErrorHandling.ConflictException>();
        exception.Which.ErrorCode.Should().Be("STATE_CONFLICT");
    }

    [Fact]
    public async Task HandleAsync_WithOpenIssue_ShouldRejectWithoutOutbox()
    {
        await using var dbContext = new InventoryDbContext(new DbContextOptionsBuilder<InventoryDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);
        var onboarding = ReadyOnboarding();
        onboarding.AddPendingIssue(Guid.NewGuid(), "Missing required evidence.", PendingOwnerType.Partner, null, null, null, DateTimeOffset.UtcNow, "staff-001");
        await dbContext.PropertyOnboardings.AddAsync(onboarding);
        await dbContext.SaveChangesAsync();
        var handler = new SubmitToCurationCommandHandler(dbContext, Mock.Of<IBusinessAuditWriter>(), new FixedClock(), Mock.Of<ICorrelationIdAccessor>(item => item.CorrelationId == "corr-001"), new SubmitToCurationCommandValidator());

        var action = () => handler.HandleAsync(new SubmitToCurationCommand(onboarding.Id, Guid.NewGuid(), "All evidence was reviewed.", "staff-001"), CancellationToken.None);

        await action.Should().ThrowAsync<LocalizeStay.SharedKernel.ErrorHandling.BusinessRuleViolationException>();
        (await dbContext.OutboxMessages.CountAsync()).Should().Be(0);
    }

    private static PropertyOnboarding ReadyOnboarding()
    {
        var onboarding = PropertyOnboarding.Create(Guid.NewGuid(), Guid.NewGuid(), "pre-001", new Property("Pousada Test", "dest-test", new Address("Rua Test", "10", null, "Centro", "Recife", "PE", "50000-000", "BR")), DateTimeOffset.Parse("2026-07-19T15:00:00Z"), TimeSpan.FromDays(10));
        foreach (var type in Enum.GetValues<ReadinessGateType>())
        {
            var evidence = new[] { new EvidenceReference(type switch { ReadinessGateType.SignedContract => EvidenceKind.Contract, ReadinessGateType.AuthorizedContact => EvidenceKind.FormalAuthorization, ReadinessGateType.OperationalChannel => EvidenceKind.Communication, _ => EvidenceKind.OfficialDocument }, "reference-001", "Validated evidence.") };
            var contract = type == ReadinessGateType.SignedContract ? new ContractReference("repository://contract-001", "LST-001", DateTimeOffset.UtcNow, ["LocalizeStay Ltda."]) : null;
            onboarding.ValidateGate(type, evidence, contract, "staff-001", DateTimeOffset.UtcNow);
        }
        return onboarding;
    }

    private sealed class FixedClock : IClock { public DateTimeOffset UtcNow => DateTimeOffset.Parse("2026-07-19T15:00:00Z"); }
}
