using AwesomeAssertions;
using FluentValidation;
using LocalizeStay.Modules.Inventory.Application.PropertyOnboardings;
using LocalizeStay.Modules.Inventory.Application.Timing;
using LocalizeStay.Modules.Inventory.Application.Upstream;
using LocalizeStay.Modules.Inventory.Application.Validation;
using LocalizeStay.Modules.Inventory.Domain.Partners;
using LocalizeStay.Modules.Inventory.Domain.PropertyOnboardings;
using LocalizeStay.Modules.Inventory.Infrastructure;
using LocalizeStay.SharedKernel.Auditing;
using LocalizeStay.SharedKernel.Correlation;
using LocalizeStay.SharedKernel.ErrorHandling;
using LocalizeStay.SharedKernel.Time;
using Microsoft.EntityFrameworkCore;
using Moq;

namespace LocalizeStay.UnitTests.Inventory;

public sealed class PropertyOnboardingCommandHandlerTests
{
    [Fact]
    public async Task HandleAsync_WithEligibleProperty_ShouldCreateSixGatesAndTargetDate()
    {
        await using var dbContext = CreateDbContext();
        var partner = Partner.Create(Guid.NewGuid(), "pre-001", "Test Hotel", null, new LegalIdentifier(LegalIdentifierType.Cnpj, "BR", "12345678000190"), new Contact("Jane Doe", "jane@example.com", "+5511999999999"), DateTimeOffset.UtcNow);
        await dbContext.Partners.AddAsync(partner);
        await dbContext.SaveChangesAsync();
        var handler = new CreatePropertyOnboardingCommandHandler(dbContext, new EligiblePreselectionValidator(), new ApprovedDestinationValidator(), new FixedBusinessCalendar(), Mock.Of<IBusinessAuditWriter>(), new FixedClock(), Mock.Of<ICorrelationIdAccessor>(accessor => accessor.CorrelationId == "corr-001"), new CreatePropertyOnboardingCommandValidator());

        var result = await handler.HandleAsync(new CreatePropertyOnboardingCommand(partner.Id, "pre-001", Property(), "staff-001"), CancellationToken.None);

        result.ReadinessGates.Should().HaveCount(6);
        result.TargetSubmissionAt.Should().Be(DateTimeOffset.Parse("2026-08-02T15:00:00Z"));
    }

    [Fact]
    public async Task HandleAsync_WithActiveCycle_ShouldThrowConflict()
    {
        await using var dbContext = CreateDbContext();
        var partner = Partner.Create(Guid.NewGuid(), "pre-001", "Test Hotel", null, new LegalIdentifier(LegalIdentifierType.Cnpj, "BR", "12345678000190"), new Contact("Jane Doe", "jane@example.com", "+5511999999999"), DateTimeOffset.UtcNow);
        await dbContext.Partners.AddAsync(partner);
        await dbContext.SaveChangesAsync();
        var handler = new CreatePropertyOnboardingCommandHandler(dbContext, new EligiblePreselectionValidator(), new ApprovedDestinationValidator(), new FixedBusinessCalendar(), Mock.Of<IBusinessAuditWriter>(), new FixedClock(), Mock.Of<ICorrelationIdAccessor>(accessor => accessor.CorrelationId == "corr-001"), new CreatePropertyOnboardingCommandValidator());
        await handler.HandleAsync(new CreatePropertyOnboardingCommand(partner.Id, "pre-001", Property(), "staff-001"), CancellationToken.None);

        var action = () => handler.HandleAsync(new CreatePropertyOnboardingCommand(partner.Id, "pre-001", Property(), "staff-001"), CancellationToken.None);

        var exception = await action.Should().ThrowAsync<ConflictException>();
        exception.Which.ErrorCode.Should().Be("ACTIVE_ONBOARDING_CYCLE_EXISTS");
    }

    [Fact]
    public async Task HandleAsync_AfterExistingCycleIsClosed_ShouldCreateANewCycleForTheSameAddress()
    {
        await using var dbContext = CreateDbContext();
        var partner = Partner.Create(Guid.NewGuid(), "pre-001", "Test Hotel", null, new LegalIdentifier(LegalIdentifierType.Cnpj, "BR", "12345678000190"), new Contact("Jane Doe", "jane@example.com", "+5511999999999"), DateTimeOffset.UtcNow);
        await dbContext.Partners.AddAsync(partner);
        await dbContext.SaveChangesAsync();
        var handler = new CreatePropertyOnboardingCommandHandler(dbContext, new EligiblePreselectionValidator(), new ApprovedDestinationValidator(), new FixedBusinessCalendar(), Mock.Of<IBusinessAuditWriter>(), new FixedClock(), Mock.Of<ICorrelationIdAccessor>(accessor => accessor.CorrelationId == "corr-001"), new CreatePropertyOnboardingCommandValidator());
        var first = await handler.HandleAsync(new CreatePropertyOnboardingCommand(partner.Id, "pre-001", Property(), "staff-001"), CancellationToken.None);
        var existing = await dbContext.PropertyOnboardings.SingleAsync(item => item.Id == first.Id);
        existing.Close(CloseReasonCode.PartnerWithdrawal, "Partner withdrew from the onboarding process.", DateTimeOffset.Parse("2026-07-20T15:00:00Z"), "staff-001");
        await dbContext.SaveChangesAsync();

        var second = await handler.HandleAsync(new CreatePropertyOnboardingCommand(partner.Id, "pre-001", Property(), "staff-001"), CancellationToken.None);

        second.Id.Should().NotBe(first.Id);
        (await dbContext.PropertyOnboardings.CountAsync()).Should().Be(2);
    }

    private static PropertyInput Property() => new("Pousada Test", "dest-test", new AddressInput("Rua Test", "10", null, "Centro", "Recife", "PE", "50000-000", "BR"));
    private static InventoryDbContext CreateDbContext() => new(new DbContextOptionsBuilder<InventoryDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);
    private sealed class EligiblePreselectionValidator : IPartnerPreselectionValidator { public Task EnsureEligibleAsync(string preselectionId, CancellationToken cancellationToken) => Task.CompletedTask; }
    private sealed class ApprovedDestinationValidator : IDestinationEligibilityValidator { public Task EnsureApprovedAsync(string destinationId, CancellationToken cancellationToken) => Task.CompletedTask; }
    private sealed class FixedBusinessCalendar : IBusinessCalendar { public DateTimeOffset AddBusinessDays(DateTimeOffset startUtc, int businessDays) => startUtc.AddDays(businessDays + 4); public bool IsWithinBusinessHoursSla(DateTimeOffset receivedAtUtc) => true; }
    private sealed class FixedClock : IClock { public DateTimeOffset UtcNow => DateTimeOffset.Parse("2026-07-19T15:00:00Z"); }
}
