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
            new ValidateCommercialOfferCommand(offer.PropertyId, Guid.NewGuid(), Validator1, offer.Revision, null),
            CancellationToken.None);

        result.Status.Should().Be("valid");
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
            new ValidateCommercialOfferCommand(offer.PropertyId, Guid.NewGuid(), Author1, offer.Revision, null),
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
            new ValidateCommercialOfferCommand(offer.PropertyId, Guid.NewGuid(), Validator1, offer.Revision + 1, null),
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

        var result = await handler.HandleAsync(
            new SubmitCommercialOfferCommand(
                offer.PropertyId,
                submissionId,
                offer.CurrentValidation!.Id,
                Submitter1,
                offer.Revision),
            CancellationToken.None);

        result.Status.Should().Be("accepted");
        result.ValidationId.Should().Be(offer.CurrentValidation.Id);
        (await dbContext.CommercialOffers.SingleAsync()).State.Should().Be(OfferState.Submitted);
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

        var first = await handler.HandleAsync(
            new SubmitCommercialOfferCommand(
                offer.PropertyId,
                submissionId,
                offer.CurrentValidation!.Id,
                Submitter1,
                offer.Revision),
            CancellationToken.None);

        var replay = await handler.HandleAsync(
            new SubmitCommercialOfferCommand(
                offer.PropertyId,
                submissionId,
                offer.CurrentValidation.Id,
                Submitter1,
                offer.Revision),
            CancellationToken.None);

        replay.Status.Should().Be(first.Status);
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

        await handler.HandleAsync(
            new SubmitCommercialOfferCommand(
                offer.PropertyId,
                submissionId,
                offer.CurrentValidation!.Id,
                Submitter1,
                offer.Revision),
            CancellationToken.None);

        var diffCommand = new SubmitCommercialOfferCommand(
            offer.PropertyId,
            submissionId,
            Guid.NewGuid(),
            Submitter1,
            offer.Revision);

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
            new SubmitCommercialOfferCommand(
                offer.PropertyId,
                Guid.NewGuid(),
                Guid.NewGuid(),
                Submitter1,
                offer.Revision),
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
            new SubmitCommercialOfferCommand(
                Guid.NewGuid(),
                Guid.NewGuid(),
                Guid.NewGuid(),
                Submitter1,
                1),
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

        var act = () => handler.HandleAsync(
            new SubmitCommercialOfferCommand(
                offer.PropertyId,
                Guid.NewGuid(),
                offer.CurrentValidation!.Id,
                Submitter1,
                offer.Revision),
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

    private sealed class FixedClock(DateTimeOffset utcNow) : IClock
    {
        public DateTimeOffset UtcNow => utcNow;
    }
}
