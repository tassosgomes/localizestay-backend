using AwesomeAssertions;
using FluentValidation;
using LocalizeStay.Modules.Inventory.Application.Partners;
using LocalizeStay.Modules.Inventory.Application.Upstream;
using LocalizeStay.Modules.Inventory.Application.Validation;
using LocalizeStay.Modules.Inventory.Domain.Partners;
using LocalizeStay.Modules.Inventory.Infrastructure;
using LocalizeStay.SharedKernel.Auditing;
using LocalizeStay.SharedKernel.Correlation;
using LocalizeStay.SharedKernel.ErrorHandling;
using LocalizeStay.SharedKernel.Time;
using Microsoft.EntityFrameworkCore;
using Moq;

namespace LocalizeStay.UnitTests.Inventory;

public sealed class PartnerCommandHandlerTests
{
    [Fact]
    public async Task HandleAsync_WithValidCommand_ShouldPersistNormalizedPartnerAndAuditIt()
    {
        await using var dbContext = CreateDbContext();
        var audit = new Mock<IBusinessAuditWriter>();
        var handler = new CreatePartnerCommandHandler(dbContext, new EligiblePreselectionValidator(), audit.Object, new FixedClock(), Mock.Of<ICorrelationIdAccessor>(accessor => accessor.CorrelationId == "corr-001"), new CreatePartnerCommandValidator());
        var command = new CreatePartnerCommand("pre-001", "Hotel Test", "Test Hotel", new LegalIdentifierInput("cnpj", "BR", "12.345.678/0001-90"), new ContactInput("Jane Doe", "jane@example.com", "+55 11 99999-9999"), "staff-001");

        var result = await handler.HandleAsync(command, CancellationToken.None);

        result.LegalIdentifier.Type.Should().Be("cnpj");
        (await dbContext.Partners.SingleAsync()).LegalIdentifier.NormalizedValue.Should().Be("12345678000190");
        audit.Verify(writer => writer.Record(It.IsAny<BusinessAuditEntry>()), Times.Once);
    }

    [Fact]
    public async Task HandleAsync_WithDuplicateLegalIdentifier_ShouldReturnConflictWithExistingPartner()
    {
        await using var dbContext = CreateDbContext();
        var existing = Partner.Create(Guid.NewGuid(), "pre-existing", "Existing Hotel", null, new LegalIdentifier(LegalIdentifierType.Cnpj, "BR", "12345678000190"), new Contact("Jane Doe", "jane@example.com", "+55 11 99999-9999"), DateTimeOffset.UtcNow);
        await dbContext.Partners.AddAsync(existing);
        await dbContext.SaveChangesAsync();
        var handler = new CreatePartnerCommandHandler(dbContext, new EligiblePreselectionValidator(), Mock.Of<IBusinessAuditWriter>(), new FixedClock(), Mock.Of<ICorrelationIdAccessor>(accessor => accessor.CorrelationId == "corr-001"), new CreatePartnerCommandValidator());
        var command = new CreatePartnerCommand("pre-001", "New Hotel", null, new LegalIdentifierInput("cnpj", "BR", "12.345.678/0001-90"), new ContactInput("John Doe", "john@example.com", "+55 11 99999-9999"), "staff-001");

        var act = () => handler.HandleAsync(command, CancellationToken.None);

        var exception = await act.Should().ThrowAsync<ConflictException>();
        exception.Which.ErrorCode.Should().Be("DUPLICATE_LEGAL_IDENTIFIER");
        exception.Which.ConflictingResourceId.Should().Be(existing.Id);
    }

    [Fact]
    public async Task HandleAsync_WithPartialUpdate_ShouldChangeOnlySuppliedFields()
    {
        await using var dbContext = CreateDbContext();
        var partner = Partner.Create(Guid.NewGuid(), "pre-001", "Original Hotel", "Original", new LegalIdentifier(LegalIdentifierType.Cnpj, "BR", "12345678000190"), new Contact("Jane Doe", "jane@example.com", "+55 11 99999-9999"), DateTimeOffset.UtcNow);
        await dbContext.Partners.AddAsync(partner);
        await dbContext.SaveChangesAsync();
        var handler = new UpdatePartnerCommandHandler(dbContext, Mock.Of<IBusinessAuditWriter>(), new FixedClock(), Mock.Of<ICorrelationIdAccessor>(accessor => accessor.CorrelationId == "corr-001"), new UpdatePartnerCommandValidator());

        var result = await handler.HandleAsync(new UpdatePartnerCommand(partner.Id, "Updated Hotel", false, null, null, null, "staff-001"), CancellationToken.None);

        result.LegalName.Should().Be("Updated Hotel");
        result.TradeName.Should().Be("Original");
        result.LegalIdentifier.Value.Should().Be("12345678000190");
    }

    private static InventoryDbContext CreateDbContext() => new(new DbContextOptionsBuilder<InventoryDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);

    private sealed class FixedClock : IClock
    {
        public DateTimeOffset UtcNow => DateTimeOffset.Parse("2026-07-19T15:00:00Z");
    }

    private sealed class EligiblePreselectionValidator : IPartnerPreselectionValidator
    {
        public Task EnsureEligibleAsync(string preselectionId, CancellationToken cancellationToken) => Task.CompletedTask;
    }
}
