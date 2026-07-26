using AwesomeAssertions;
using LocalizeStay.Contracts.Curation;
using LocalizeStay.Modules.Inventory.Application.CommercialOffers;
using LocalizeStay.Modules.Inventory.Domain.CommercialOffers;
using LocalizeStay.Modules.Inventory.Infrastructure;
using LocalizeStay.SharedKernel.Auditing;
using LocalizeStay.SharedKernel.Correlation;
using LocalizeStay.SharedKernel.Time;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Moq;

namespace LocalizeStay.UnitTests.Inventory;

public sealed class CurationOfferReturnedHandlerTests
{
    private static readonly DateTimeOffset _now = DateTimeOffset.Parse("2026-07-22T10:00:00Z");
    private const string Validator1 = "staff-beta";
    private const string Submitter1 = "staff-gamma";
    private const string Author1 = "staff-alpha";
    private const string Curator1 = "curator-001";

    [Fact]
    public async Task HandleAsync_WithValidSubmission_ShouldRecordReturn()
    {
        await using var dbContext = CreateDbContext();
        var propertyId = Guid.NewGuid();
        var eventId = Guid.NewGuid();
        var (offer, actualSubmissionId) = await SeedSubmittedOffer(dbContext, propertyId);

        var handler = CreateHandler(dbContext);

        var integrationEvent = new CurationOfferReturnedV1
        {
            EventId = eventId,
            PropertyId = propertyId,
            SubmissionId = actualSubmissionId,
            Revision = offer.Revision,
            ReasonCode = "incomplete_data",
            Reason = "Missing accommodation details.",
            ReturnedBy = Curator1,
            ReturnedAt = _now,
            CorrelationId = "corr-001",
        };

        await handler.HandleAsync(integrationEvent, CancellationToken.None);

        var reloaded = await dbContext.CommercialOffers
            .SingleOrDefaultAsync(o => o.PropertyId == propertyId);

        reloaded!.State.Should().Be(OfferState.Returned);
        reloaded.Returns.Should().ContainSingle();
        reloaded.Returns[0].EventId.Should().Be(eventId);
    }

    [Fact]
    public async Task HandleAsync_DuplicateEvent_ShouldBeIgnored()
    {
        await using var dbContext = CreateDbContext();
        var propertyId = Guid.NewGuid();
        var (offer, actualSubmissionId) = await SeedSubmittedOffer(dbContext, propertyId);

        var handler = CreateHandler(dbContext);

        var integrationEvent = new CurationOfferReturnedV1
        {
            PropertyId = propertyId,
            SubmissionId = actualSubmissionId,
            Revision = offer.Revision,
            ReasonCode = "incomplete_data",
            Reason = "Missing details.",
            ReturnedBy = Curator1,
            ReturnedAt = _now,
            CorrelationId = "corr-001",
        };

        await handler.HandleAsync(integrationEvent, CancellationToken.None);
        await handler.HandleAsync(integrationEvent, CancellationToken.None);

        var reloaded = await dbContext.CommercialOffers
            .SingleOrDefaultAsync(o => o.PropertyId == propertyId);

        reloaded!.Returns.Count.Should().Be(1);
    }

    [Fact]
    public async Task HandleAsync_NonexistentProperty_ShouldBeIgnored()
    {
        await using var dbContext = CreateDbContext();
        var handler = CreateHandler(dbContext);

        var integrationEvent = new CurationOfferReturnedV1
        {
            PropertyId = Guid.NewGuid(),
            SubmissionId = Guid.NewGuid(),
            Revision = 1,
            ReasonCode = "incomplete_data",
            Reason = "Missing details.",
            ReturnedBy = Curator1,
            ReturnedAt = _now,
            CorrelationId = "corr-001",
        };

        await handler.HandleAsync(integrationEvent, CancellationToken.None);

        (await dbContext.OfferReturns.CountAsync()).Should().Be(0);
    }

    [Fact]
    public async Task HandleAsync_AlreadyReturnedOffer_ShouldBeIgnored()
    {
        await using var dbContext = CreateDbContext();
        var propertyId = Guid.NewGuid();
        var (offer, actualSubmissionId) = await SeedSubmittedOffer(dbContext, propertyId);

        var handler = CreateHandler(dbContext);

        var firstEvent = new CurationOfferReturnedV1
        {
            PropertyId = propertyId,
            SubmissionId = actualSubmissionId,
            Revision = offer.Revision,
            ReasonCode = "incomplete_data",
            Reason = "First return.",
            ReturnedBy = Curator1,
            ReturnedAt = _now,
            CorrelationId = "corr-001",
        };

        await handler.HandleAsync(firstEvent, CancellationToken.None);

        var secondEvent = new CurationOfferReturnedV1
        {
            EventId = Guid.NewGuid(),
            PropertyId = propertyId,
            SubmissionId = actualSubmissionId,
            Revision = offer.Revision,
            ReasonCode = "another_issue",
            Reason = "Second return attempt.",
            ReturnedBy = Curator1,
            ReturnedAt = _now,
            CorrelationId = "corr-002",
        };

        await handler.HandleAsync(secondEvent, CancellationToken.None);

        var reloaded = await dbContext.CommercialOffers
            .SingleOrDefaultAsync(o => o.PropertyId == propertyId);

        reloaded!.Returns.Count.Should().Be(1);
    }

    [Fact]
    public async Task HandleAsync_PublishedOffer_ShouldThrow()
    {
        await using var dbContext = CreateDbContext();
        var propertyId = Guid.NewGuid();
        var (offer, actualSubmissionId) = await SeedSubmittedOffer(dbContext, propertyId);
        offer.MarkPublished(_now);
        await dbContext.SaveChangesAsync();

        var handler = CreateHandler(dbContext);

        var integrationEvent = new CurationOfferReturnedV1
        {
            PropertyId = propertyId,
            SubmissionId = actualSubmissionId,
            Revision = offer.Revision,
            ReasonCode = "incomplete_data",
            Reason = "Missing details.",
            ReturnedBy = Curator1,
            ReturnedAt = _now,
            CorrelationId = "corr-001",
        };

        var act = () => handler.HandleAsync(integrationEvent, CancellationToken.None);

        await act.Should().ThrowAsync<LocalizeStay.SharedKernel.ErrorHandling.BusinessRuleViolationException>()
            .Where(ex => ex.ErrorCode == "PUBLISHED_OFFER_CHANGE_REQUIRES_F04");
    }

    [Fact]
    public async Task HandleAsync_AfterRevalidation_ReturnToPriorSubmittedShouldBeIgnored()
    {
        await using var dbContext = CreateDbContext();
        var propertyId = Guid.NewGuid();
        var (offer, actualSubmissionId) = await SeedSubmittedOffer(dbContext, propertyId);

        var handler = CreateHandler(dbContext);

        var firstEvent = new CurationOfferReturnedV1
        {
            PropertyId = propertyId,
            SubmissionId = actualSubmissionId,
            Revision = offer.Revision,
            ReasonCode = "incomplete_data",
            Reason = "First return.",
            ReturnedBy = Curator1,
            ReturnedAt = _now,
            CorrelationId = "corr-001",
        };

        await handler.HandleAsync(firstEvent, CancellationToken.None);

        // Operator corrects and re-submits (new submission)
        var reOffer = await dbContext.CommercialOffers.SingleOrDefaultAsync(o => o.PropertyId == propertyId);
        reOffer!.IncrementRevisionMutate(Author1, _now, null, () => { });
        reOffer.RecalculateCompleteness(2, 2, 2, false, _now);
        var newValidationId = Guid.NewGuid();
        reOffer.Validate(newValidationId, Validator1, reOffer.Revision, _now);
        reOffer.Submit(Guid.NewGuid(), "{}", Submitter1, reOffer.Revision, _now);
        await dbContext.SaveChangesAsync();

        // Duplicate of the first return event should be ignored (already processed)
        await handler.HandleAsync(firstEvent, CancellationToken.None);

        var reloaded = await dbContext.CommercialOffers
            .SingleOrDefaultAsync(o => o.PropertyId == propertyId);

        reloaded!.State.Should().Be(OfferState.Submitted);
        reloaded.Returns.Count.Should().Be(1);
        reloaded.Submissions.Count.Should().Be(2);
    }

    private static InventoryDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<InventoryDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        return new InventoryDbContext(options);
    }

    private static async Task<(CommercialOffer Offer, Guid SubmissionId)> SeedSubmittedOffer(InventoryDbContext dbContext, Guid propertyId)
    {
        var offer = CommercialOffer.Create(propertyId, Author1, _now);
        offer.RecalculateCompleteness(2, 2, 2, false, _now);
        var validationId = Guid.NewGuid();
        offer.Validate(validationId, Validator1, offer.Revision, _now);
        var submissionId = Guid.NewGuid();
        offer.Submit(submissionId, "{}", Submitter1, offer.Revision, _now);
        await dbContext.CommercialOffers.AddAsync(offer);
        await dbContext.SaveChangesAsync();
        return (offer, submissionId);
    }

    private static CurationOfferReturnedHandler CreateHandler(InventoryDbContext dbContext)
    {
        return new CurationOfferReturnedHandler(
            dbContext,
            Mock.Of<IBusinessAuditWriter>(),
            new FixedClock(_now),
            Mock.Of<ICorrelationIdAccessor>(a => a.CorrelationId == "corr-001"),
            Mock.Of<ILogger<CurationOfferReturnedHandler>>());
    }

    private sealed class FixedClock(DateTimeOffset utcNow) : IClock
    {
        public DateTimeOffset UtcNow => utcNow;
    }
}
