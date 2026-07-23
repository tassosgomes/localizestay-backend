using System.Text.Encodings.Web;
using System.Text.Json;
using AwesomeAssertions;
using LocalizeStay.Modules.Inventory.Application.CommercialOffers;
using LocalizeStay.Modules.Inventory.Domain.CommercialOffers;
using LocalizeStay.Modules.Inventory.Infrastructure;
using LocalizeStay.SharedKernel.Auditing;
using LocalizeStay.SharedKernel.Correlation;
using LocalizeStay.SharedKernel.Time;
using Microsoft.EntityFrameworkCore;
using Moq;

namespace LocalizeStay.UnitTests.Inventory;

public sealed class CommercialOfferCommandHandlerTests
{
    private static readonly DateTimeOffset _now = DateTimeOffset.Parse("2026-07-22T10:00:00Z");
    private const string Validator1 = "staff-beta";
    private const string Submitter1 = "staff-gamma";
    private const string Author1 = "staff-alpha";

    [Fact]
    public async Task ValidateHandler_WithReadyOffer_ShouldSetStateToValidated()
    {
        await using var dbContext = CreateDbContext();
        var offer = await SeedReadyOffer(dbContext);
        var handler = new CreateOfferValidationCommandHandler(
            dbContext,
            Mock.Of<IBusinessAuditWriter>(),
            new FixedClock(_now),
            Mock.Of<ICorrelationIdAccessor>(a => a.CorrelationId == "corr-001"),
            new ValidateCommercialOfferCommandValidator());

        var result = await handler.HandleAsync(
            new ValidateCommercialOfferCommand(offer.PropertyId, Guid.NewGuid(), Validator1, offer.Revision),
            CancellationToken.None);

        result.State.Should().Be("validated");
        var reloaded = await dbContext.CommercialOffers.SingleAsync(o => o.PropertyId == offer.PropertyId);
        reloaded.State.Should().Be(OfferState.Validated);
        reloaded.CurrentValidation.Should().NotBeNull();
    }

    [Fact]
    public async Task ValidateHandler_SelfValidation_ShouldThrow()
    {
        await using var dbContext = CreateDbContext();
        var offer = await SeedReadyOffer(dbContext);
        var handler = new CreateOfferValidationCommandHandler(
            dbContext,
            Mock.Of<IBusinessAuditWriter>(),
            new FixedClock(_now),
            Mock.Of<ICorrelationIdAccessor>(a => a.CorrelationId == "corr-001"),
            new ValidateCommercialOfferCommandValidator());

        var act = () => handler.HandleAsync(
            new ValidateCommercialOfferCommand(offer.PropertyId, Guid.NewGuid(), Author1, offer.Revision),
            CancellationToken.None);

        await act.Should().ThrowAsync<LocalizeStay.SharedKernel.ErrorHandling.BusinessRuleViolationException>()
            .Where(ex => ex.ErrorCode == "SELF_VALIDATION_NOT_ALLOWED");
    }

    [Fact]
    public async Task ValidateHandler_RevisionMismatch_ShouldThrow()
    {
        await using var dbContext = CreateDbContext();
        var offer = await SeedReadyOffer(dbContext);
        var handler = new CreateOfferValidationCommandHandler(
            dbContext,
            Mock.Of<IBusinessAuditWriter>(),
            new FixedClock(_now),
            Mock.Of<ICorrelationIdAccessor>(a => a.CorrelationId == "corr-001"),
            new ValidateCommercialOfferCommandValidator());

        var act = () => handler.HandleAsync(
            new ValidateCommercialOfferCommand(offer.PropertyId, Guid.NewGuid(), Validator1, offer.Revision + 1),
            CancellationToken.None);

        await act.Should().ThrowAsync<LocalizeStay.SharedKernel.ErrorHandling.BusinessRuleViolationException>()
            .Where(ex => ex.ErrorCode == "REVISION_MISMATCH");
    }

    [Fact]
    public async Task SubmitHandler_WithValidValidation_ShouldPersistStateAndOutbox()
    {
        await using var dbContext = CreateDbContext();
        var offer = await SeedValidatedOffer(dbContext);
        var handler = new SubmitCommercialOfferCommandHandler(
            dbContext,
            Mock.Of<IBusinessAuditWriter>(),
            new FixedClock(_now),
            Mock.Of<ICorrelationIdAccessor>(a => a.CorrelationId == "corr-001"),
            new SubmitCommercialOfferCommandValidator());

        var submissionId = Guid.NewGuid();
        var snapshotJson = SerializeTestSnapshot(offer, Submitter1);

        var result = await handler.HandleAsync(
            new SubmitCommercialOfferCommand(offer.PropertyId, submissionId, snapshotJson, Submitter1, offer.Revision),
            CancellationToken.None);

        result.State.Should().Be("submitted");
        result.EverSubmitted.Should().BeTrue();
        (await dbContext.CommercialOfferIdempotencyKeys.CountAsync()).Should().Be(1);
        (await dbContext.OutboxMessages.CountAsync()).Should().Be(1);
    }

    [Fact]
    public async Task SubmitHandler_ReplayIdempotent_ShouldReturnSameResult()
    {
        await using var dbContext = CreateDbContext();
        var offer = await SeedValidatedOffer(dbContext);
        var handler = new SubmitCommercialOfferCommandHandler(
            dbContext,
            Mock.Of<IBusinessAuditWriter>(),
            new FixedClock(_now),
            Mock.Of<ICorrelationIdAccessor>(a => a.CorrelationId == "corr-001"),
            new SubmitCommercialOfferCommandValidator());

        var submissionId = Guid.NewGuid();
        var snapshotJson = SerializeTestSnapshot(offer, Submitter1);

        var first = await handler.HandleAsync(
            new SubmitCommercialOfferCommand(offer.PropertyId, submissionId, snapshotJson, Submitter1, offer.Revision),
            CancellationToken.None);

        var replay = await handler.HandleAsync(
            new SubmitCommercialOfferCommand(offer.PropertyId, submissionId, snapshotJson, Submitter1, offer.Revision),
            CancellationToken.None);

        replay.State.Should().Be(first.State);
        replay.Revision.Should().Be(first.Revision);
    }

    [Fact]
    public async Task SubmitHandler_DifferentFingerprint_ShouldReturnIdempotencyKeyReused()
    {
        await using var dbContext = CreateDbContext();
        var offer = await SeedValidatedOffer(dbContext);
        var handler = new SubmitCommercialOfferCommandHandler(
            dbContext,
            Mock.Of<IBusinessAuditWriter>(),
            new FixedClock(_now),
            Mock.Of<ICorrelationIdAccessor>(a => a.CorrelationId == "corr-001"),
            new SubmitCommercialOfferCommandValidator());

        var submissionId = Guid.NewGuid();
        var snapshotJson = SerializeTestSnapshot(offer, Submitter1);

        await handler.HandleAsync(
            new SubmitCommercialOfferCommand(offer.PropertyId, submissionId, snapshotJson, Submitter1, offer.Revision),
            CancellationToken.None);

        var diffCommand = new SubmitCommercialOfferCommand(offer.PropertyId, submissionId, "{}", Submitter1, offer.Revision);

        var act = () => handler.HandleAsync(diffCommand, CancellationToken.None);

        await act.Should().ThrowAsync<LocalizeStay.SharedKernel.ErrorHandling.ConflictException>()
            .Where(ex => ex.ErrorCode == "IDEMPOTENCY_KEY_REUSED");
    }

    [Fact]
    public async Task SubmitHandler_WithoutValidation_ShouldThrowValidationRequired()
    {
        await using var dbContext = CreateDbContext();
        var offer = await SeedReadyOffer(dbContext);
        var handler = new SubmitCommercialOfferCommandHandler(
            dbContext,
            Mock.Of<IBusinessAuditWriter>(),
            new FixedClock(_now),
            Mock.Of<ICorrelationIdAccessor>(a => a.CorrelationId == "corr-001"),
            new SubmitCommercialOfferCommandValidator());

        var act = () => handler.HandleAsync(
            new SubmitCommercialOfferCommand(offer.PropertyId, Guid.NewGuid(), "{}", Submitter1, offer.Revision),
            CancellationToken.None);

        await act.Should().ThrowAsync<LocalizeStay.SharedKernel.ErrorHandling.BusinessRuleViolationException>()
            .Where(ex => ex.ErrorCode == "VALIDATION_REQUIRED");
    }

    [Fact]
    public async Task SubmitHandler_NonexistentOffer_ShouldThrowNotFound()
    {
        await using var dbContext = CreateDbContext();
        var handler = new SubmitCommercialOfferCommandHandler(
            dbContext,
            Mock.Of<IBusinessAuditWriter>(),
            new FixedClock(_now),
            Mock.Of<ICorrelationIdAccessor>(a => a.CorrelationId == "corr-001"),
            new SubmitCommercialOfferCommandValidator());

        var act = () => handler.HandleAsync(
            new SubmitCommercialOfferCommand(Guid.NewGuid(), Guid.NewGuid(), "{}", Submitter1, 1),
            CancellationToken.None);

        await act.Should().ThrowAsync<LocalizeStay.SharedKernel.ErrorHandling.NotFoundException>();
    }

    [Fact]
    public async Task SubmitHandler_PublishedOffer_ShouldThrow()
    {
        await using var dbContext = CreateDbContext();
        var offer = await SeedValidatedOffer(dbContext);
        offer.MarkPublished(_now);
        await dbContext.SaveChangesAsync();

        var handler = new SubmitCommercialOfferCommandHandler(
            dbContext,
            Mock.Of<IBusinessAuditWriter>(),
            new FixedClock(_now),
            Mock.Of<ICorrelationIdAccessor>(a => a.CorrelationId == "corr-001"),
            new SubmitCommercialOfferCommandValidator());

        var snapshotJson = SerializeTestSnapshot(offer, Submitter1);
        var act = () => handler.HandleAsync(
            new SubmitCommercialOfferCommand(offer.PropertyId, Guid.NewGuid(), snapshotJson, Submitter1, offer.Revision),
            CancellationToken.None);

        await act.Should().ThrowAsync<LocalizeStay.SharedKernel.ErrorHandling.BusinessRuleViolationException>()
            .Where(ex => ex.ErrorCode == "PUBLISHED_OFFER_CHANGE_REQUIRES_F04");
    }

    private static InventoryDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<InventoryDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        return new InventoryDbContext(options);
    }

    private static async Task<CommercialOffer> SeedReadyOffer(InventoryDbContext dbContext)
    {
        var offer = CommercialOffer.Create(Guid.NewGuid(), Author1, _now);
        offer.RecalculateCompleteness(2, 2, 2, false, _now);
        await dbContext.CommercialOffers.AddAsync(offer);
        await dbContext.SaveChangesAsync();
        return offer;
    }

    private static async Task<CommercialOffer> SeedValidatedOffer(InventoryDbContext dbContext)
    {
        var offer = await SeedReadyOffer(dbContext);
        var validationId = Guid.NewGuid();
        offer.Validate(validationId, Validator1, offer.Revision, _now);
        await dbContext.SaveChangesAsync();
        return offer;
    }

    private static string SerializeTestSnapshot(CommercialOffer offer, string submittedBy)
    {
        var snapshot = new
        {
            snapshotVersion = 1,
            offer.Id,
            offer.PropertyId,
            revision = offer.Revision,
            revisionAuthor = offer.RevisionAuthor,
            state = offer.State.ToString(),
            submittedBy,
            submittedAt = _now,
            accommodations = Array.Empty<object>(),
            policies = Array.Empty<object>(),
        };

        return JsonSerializer.Serialize(snapshot, new JsonSerializerOptions
        {
            WriteIndented = false,
            Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        });
    }

    private sealed class FixedClock(DateTimeOffset utcNow) : IClock
    {
        public DateTimeOffset UtcNow => utcNow;
    }
}
