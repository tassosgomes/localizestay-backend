using FluentValidation;
using LocalizeStay.Modules.Inventory.Domain.PropertyOnboardings;
using LocalizeStay.Modules.Inventory.Infrastructure;
using LocalizeStay.SharedKernel.Auditing;
using LocalizeStay.SharedKernel.Correlation;
using LocalizeStay.SharedKernel.Cqrs;
using LocalizeStay.SharedKernel.ErrorHandling;
using LocalizeStay.SharedKernel.Time;
using Microsoft.EntityFrameworkCore;

namespace LocalizeStay.Modules.Inventory.Application.PropertyOnboardings;

internal sealed record CreateDuplicateReviewCommand(Guid OnboardingId, Guid IdempotencyKey, string Decision, Guid? ExistingPropertyId, string Justification, string Actor) : ICommand<DuplicateReviewResultResponse>;

internal sealed class CreateDuplicateReviewCommandHandler(
    InventoryDbContext dbContext,
    IBusinessAuditWriter auditWriter,
    IClock clock,
    ICorrelationIdAccessor correlationIdAccessor,
    IValidator<CreateDuplicateReviewCommand> validator) : ICommandHandler<CreateDuplicateReviewCommand, DuplicateReviewResultResponse>
{
    public async Task<DuplicateReviewResultResponse> HandleAsync(CreateDuplicateReviewCommand command, CancellationToken cancellationToken)
    {
        await validator.ValidateAndThrowAsync(command, cancellationToken);
        var existingKey = await dbContext.IdempotencyKeys.AsNoTracking().SingleOrDefaultAsync(key => key.Key == command.IdempotencyKey, cancellationToken);
        if (existingKey is not null)
        {
            if (existingKey.PropertyOnboardingId != command.OnboardingId || existingKey.Scope != IdempotencyScope.DuplicateReview)
                throw new ConflictException("Idempotency key was already used for a different operation.", "IDEMPOTENCY_KEY_CONFLICT");
            var replay = await dbContext.PropertyOnboardings.AsNoTracking().Include(item => item.ReadinessGates).Include(item => item.PendingIssues).Include(item => item.DuplicateReviews).SingleAsync(item => item.Id == command.OnboardingId, cancellationToken);
            var replayReview = replay.DuplicateReviews.OrderByDescending(item => item.CreatedAt).First();
            return new DuplicateReviewResultResponse(PropertyOnboardingMapper.ToDuplicateReviewResponse(replayReview), PropertyOnboardingMapper.ToResponse(replay));
        }

        var onboarding = await dbContext.PropertyOnboardings.Include(item => item.ReadinessGates).Include(item => item.PendingIssues).Include(item => item.DuplicateReviews).SingleOrDefaultAsync(item => item.Id == command.OnboardingId, cancellationToken)
            ?? throw new NotFoundException("Property onboarding was not found.", "PROPERTY_ONBOARDING_NOT_FOUND");
        var now = clock.UtcNow;
        var review = onboarding.SubmitDuplicateReview(Guid.NewGuid(), Enum.Parse<DuplicateReviewDecision>(command.Decision, true), command.ExistingPropertyId, command.Justification, now, command.Actor, command.IdempotencyKey);
        await dbContext.IdempotencyKeys.AddAsync(IdempotencyKey.Create(onboarding.Id, command.IdempotencyKey, IdempotencyScope.DuplicateReview, now), cancellationToken);
        auditWriter.Record(BusinessAuditEntry.Create("PropertyOnboarding", onboarding.Id.ToString(), command.Actor, "DuplicateReviewed", "Duplicate review recorded.", now, correlationIdAccessor.CorrelationId, new Dictionary<string, string> { ["onboardingId"] = onboarding.Id.ToString(), ["reviewId"] = review.Id.ToString(), ["idempotencyKey"] = command.IdempotencyKey.ToString() }));
        await dbContext.SaveChangesAsync(cancellationToken);
        return new DuplicateReviewResultResponse(PropertyOnboardingMapper.ToDuplicateReviewResponse(review), PropertyOnboardingMapper.ToResponse(onboarding));
    }
}
