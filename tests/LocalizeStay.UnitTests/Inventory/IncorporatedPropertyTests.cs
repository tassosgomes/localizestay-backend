using AwesomeAssertions;
using LocalizeStay.Modules.Inventory.Domain.IncorporatedProperties;
using LocalizeStay.SharedKernel.ErrorHandling;

namespace LocalizeStay.UnitTests.Inventory;

public sealed class IncorporatedPropertyTests
{
    [Fact]
    public void Create_WithValidInputs_ShouldAssignIdentityAndTimestamps()
    {
        var id = Guid.NewGuid();
        var partnerId = Guid.NewGuid();
        var now = DateTimeOffset.Parse("2026-07-22T10:00:00Z");

        var property = IncorporatedProperty.Create(
            id,
            partnerId,
            "Pousada Test",
            "dest-test",
            "staff-001",
            now);

        property.Id.Should().Be(id);
        property.OnboardingId.Should().Be(id);
        property.PartnerId.Should().Be(partnerId);
        property.PropertyName.Should().Be("Pousada Test");
        property.DestinationId.Should().Be("dest-test");
        property.InitialActor.Should().Be("staff-001");
        property.CreatedAt.Should().Be(now);
        property.UpdatedAt.Should().Be(now);
    }

    [Fact]
    public void Create_WithBlankPropertyName_ShouldThrow()
    {
        var act = () => IncorporatedProperty.Create(
            Guid.NewGuid(),
            Guid.NewGuid(),
            "   ",
            "dest-test",
            "staff-001",
            DateTimeOffset.UtcNow);

        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Create_WithPropertyNameTooLong_ShouldThrow()
    {
        var longName = new string('a', 181);

        var act = () => IncorporatedProperty.Create(
            Guid.NewGuid(),
            Guid.NewGuid(),
            longName,
            "dest-test",
            "staff-001",
            DateTimeOffset.UtcNow);

        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Create_WithDestinationIdTooLong_ShouldThrow()
    {
        var longDest = new string('a', 121);

        var act = () => IncorporatedProperty.Create(
            Guid.NewGuid(),
            Guid.NewGuid(),
            "Valid Name",
            longDest,
            "staff-001",
            DateTimeOffset.UtcNow);

        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Create_WithBlankInitialActor_ShouldThrow()
    {
        var act = () => IncorporatedProperty.Create(
            Guid.NewGuid(),
            Guid.NewGuid(),
            "Valid Name",
            "dest-test",
            "   ",
            DateTimeOffset.UtcNow);

        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Create_TrimsInputs()
    {
        var property = IncorporatedProperty.Create(
            Guid.NewGuid(),
            Guid.NewGuid(),
            "  Pousada Test  ",
            "  dest-test  ",
            "  staff-001  ",
            DateTimeOffset.UtcNow);

        property.PropertyName.Should().Be("Pousada Test");
        property.DestinationId.Should().Be("dest-test");
        property.InitialActor.Should().Be("staff-001");
    }

    [Fact]
    public void Sync_ShouldUpdateMutableFields()
    {
        var now = DateTimeOffset.UtcNow;
        var property = IncorporatedProperty.Create(
            Guid.NewGuid(),
            Guid.NewGuid(),
            "Old Name",
            "old-dest",
            "staff-001",
            now);
        var later = now.AddMinutes(5);

        property.Sync("New Name", "new-dest", later);

        property.PropertyName.Should().Be("New Name");
        property.DestinationId.Should().Be("new-dest");
        property.UpdatedAt.Should().Be(later);
        property.Id.Should().Be(property.OnboardingId);
    }

    [Fact]
    public void Sync_ShouldNotChangeIdentity()
    {
        var id = Guid.NewGuid();
        var partnerId = Guid.NewGuid();
        var now = DateTimeOffset.UtcNow;
        var property = IncorporatedProperty.Create(
            id,
            partnerId,
            "Original",
            "dest-original",
            "staff-001",
            now);
        var later = now.AddMinutes(5);

        property.Sync("Updated", "dest-updated", later);

        property.Id.Should().Be(id);
        property.OnboardingId.Should().Be(id);
        property.PartnerId.Should().Be(partnerId);
        property.InitialActor.Should().Be("staff-001");
        property.CreatedAt.Should().Be(now);
    }

    [Fact]
    public void Sync_WithOlderTimestamp_ShouldThrowStaleSync()
    {
        var now = DateTimeOffset.UtcNow;
        var property = IncorporatedProperty.Create(
            Guid.NewGuid(),
            Guid.NewGuid(),
            "Name",
            "dest",
            "staff-001",
            now);
        var older = now.AddMinutes(-5);

        var act = () => property.Sync("New Name", "new-dest", older);

        act.Should().Throw<BusinessRuleViolationException>()
            .Where(ex => ex.ErrorCode == "INCORPORATED_PROPERTY_STALE_SYNC");
    }

    [Fact]
    public void Sync_WithBlankPropertyName_ShouldThrow()
    {
        var property = IncorporatedProperty.Create(
            Guid.NewGuid(),
            Guid.NewGuid(),
            "Name",
            "dest",
            "staff-001",
            DateTimeOffset.UtcNow);

        var act = () => property.Sync("   ", "dest", DateTimeOffset.UtcNow);

        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Idempotency_DoubleCreateWithSameId_ShouldProduceIdenticalEntity()
    {
        var id = Guid.NewGuid();
        var partnerId = Guid.NewGuid();
        var now = DateTimeOffset.Parse("2026-07-22T10:00:00Z");

        var first = IncorporatedProperty.Create(id, partnerId, "Pousada Test", "dest-test", "staff-001", now);
        var second = IncorporatedProperty.Create(id, partnerId, "Pousada Test", "dest-test", "staff-001", now);

        first.Id.Should().Be(second.Id);
        first.OnboardingId.Should().Be(second.OnboardingId);
        first.PartnerId.Should().Be(second.PartnerId);
        first.PropertyName.Should().Be(second.PropertyName);
        first.InitialActor.Should().Be(second.InitialActor);
    }

    [Fact]
    public void Sync_MultipleCallsWithSameData_ShouldBeIdempotent()
    {
        var property = IncorporatedProperty.Create(
            Guid.NewGuid(),
            Guid.NewGuid(),
            "Name",
            "dest",
            "staff-001",
            DateTimeOffset.UtcNow);
        var later = DateTimeOffset.UtcNow;
        property.Sync("Name", "dest", later);

        var act = () => property.Sync("Name", "dest", later.AddSeconds(1));

        act.Should().NotThrow();
    }

    [Fact]
    public void Sync_AfterCreate_SameData_ShouldUpdateTimestamp()
    {
        var now = DateTimeOffset.Parse("2026-07-22T10:00:00Z");
        var property = IncorporatedProperty.Create(
            Guid.NewGuid(),
            Guid.NewGuid(),
            "Name",
            "dest",
            "staff-001",
            now);
        var later = now.AddSeconds(10);

        property.Sync("Name", "dest", later);

        property.UpdatedAt.Should().Be(later);
        property.CreatedAt.Should().Be(now);
    }
}
