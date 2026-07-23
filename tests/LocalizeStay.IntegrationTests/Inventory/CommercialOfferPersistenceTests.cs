using System.Text.Json;
using AwesomeAssertions;
using LocalizeStay.IntegrationTests.Infrastructure;
using LocalizeStay.Modules.Inventory.Domain.CommercialOffers;
using LocalizeStay.Modules.Inventory.Domain.IncorporatedProperties;
using LocalizeStay.Modules.Inventory.Domain.Partners;
using LocalizeStay.Modules.Inventory.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace LocalizeStay.IntegrationTests.Inventory;

public sealed class CommercialOfferPersistenceTests : IClassFixture<LocalizeStayWebApplicationFactory>
{
    private readonly LocalizeStayWebApplicationFactory _factory;

    public CommercialOfferPersistenceTests(LocalizeStayWebApplicationFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task CommercialOffer_Creates_And_Saves_Successfully()
    {
        using var scope = _factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<InventoryDbContext>();
        await dbContext.Database.MigrateAsync();
        await ClearCommercialOfferDataAsync(dbContext);

        var now = DateTimeOffset.UtcNow;
        var incorporatedProperty = IncorporatedProperty.Create(
            Guid.NewGuid(),
            Guid.NewGuid(),
            "Hotel Test",
            "dest-001",
            "staff-test",
            now);

        await dbContext.IncorporatedProperties.AddAsync(incorporatedProperty);
        await dbContext.SaveChangesAsync();

        var offer = CommercialOffer.Create(incorporatedProperty.Id, "staff-test", now);

        await dbContext.CommercialOffers.AddAsync(offer);
        await dbContext.SaveChangesAsync();
        dbContext.ChangeTracker.Clear();

        var loaded = await dbContext.CommercialOffers
            .AsNoTracking()
            .SingleOrDefaultAsync(o => o.Id == offer.Id);

        loaded.Should().NotBeNull();
        loaded!.State.Should().Be(OfferState.Draft);
        loaded.Revision.Should().Be(1);
        loaded.AccommodationCount.Should().Be(0);
    }

    [Fact]
    public async Task CommercialOffer_WithoutIncorporatedProperty_FailsOnForeignKey()
    {
        using var scope = _factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<InventoryDbContext>();
        await dbContext.Database.MigrateAsync();
        await ClearCommercialOfferDataAsync(dbContext);

        var offer = CommercialOffer.Create(Guid.NewGuid(), "staff-test", DateTimeOffset.UtcNow);

        await dbContext.CommercialOffers.AddAsync(offer);
        var action = () => dbContext.SaveChangesAsync();

        await action.Should().ThrowAsync<DbUpdateException>();
    }

    [Fact]
    public async Task CommercialOffer_DuplicateId_FailsOnPrimaryKey()
    {
        using var scope = _factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<InventoryDbContext>();
        await dbContext.Database.MigrateAsync();
        await ClearCommercialOfferDataAsync(dbContext);

        var now = DateTimeOffset.UtcNow;
        var propertyId = Guid.NewGuid();

        var incorporatedProperty = IncorporatedProperty.Create(
            propertyId, Guid.NewGuid(), "Hotel Test", "dest-001", "staff-test", now);
        await dbContext.IncorporatedProperties.AddAsync(incorporatedProperty);
        await dbContext.SaveChangesAsync();

        var first = CommercialOffer.Create(propertyId, "staff-test", now);
        var second = CommercialOffer.Create(propertyId, "staff-test", now);

        await dbContext.CommercialOffers.AddAsync(first);
        await dbContext.SaveChangesAsync();
        dbContext.ChangeTracker.Clear();

        await dbContext.CommercialOffers.AddAsync(second);
        var action = () => dbContext.SaveChangesAsync();

        await action.Should().ThrowAsync<DbUpdateException>();
    }

    [Fact]
    public async Task Accommodation_BedConfiguration_RoundTripsAsJson()
    {
        using var scope = _factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<InventoryDbContext>();
        await dbContext.Database.MigrateAsync();
        await ClearCommercialOfferDataAsync(dbContext);

        var now = DateTimeOffset.UtcNow;
        var propertyId = Guid.NewGuid();

        var incorporatedProperty = IncorporatedProperty.Create(
            propertyId, Guid.NewGuid(), "Hotel Test", "dest-001", "staff-test", now);
        await dbContext.IncorporatedProperties.AddAsync(incorporatedProperty);

        var offer = CommercialOffer.Create(propertyId, "staff-test", now);
        await dbContext.CommercialOffers.AddAsync(offer);

        var accommodation = Accommodation.Create(
            Guid.NewGuid(), propertyId, "Suite Master", null, null, now);
        accommodation.SetBedConfiguration(
        [
            BedEntry.Create(BedType.King, 1),
            BedEntry.Create(BedType.Single, 2),
        ]);

        await dbContext.Accommodations.AddAsync(accommodation);
        await dbContext.SaveChangesAsync();
        dbContext.ChangeTracker.Clear();

        var loaded = await dbContext.Accommodations
            .AsNoTracking()
            .SingleOrDefaultAsync(a => a.Id == accommodation.Id);

        loaded.Should().NotBeNull();
        loaded!.BedConfiguration.Should().HaveCount(2);
        loaded.BedConfiguration.Should().Contain(b => b.Type == BedType.King && b.Count == 1);
        loaded.BedConfiguration.Should().Contain(b => b.Type == BedType.Single && b.Count == 2);
    }

    [Fact]
    public async Task OfferSubmission_SnapshotJson_PersistsAsJsonb()
    {
        using var scope = _factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<InventoryDbContext>();
        await dbContext.Database.MigrateAsync();
        await ClearCommercialOfferDataAsync(dbContext);

        var now = DateTimeOffset.UtcNow;
        var propertyId = Guid.NewGuid();

        var incorporatedProperty = IncorporatedProperty.Create(
            propertyId, Guid.NewGuid(), "Hotel Test", "dest-001", "staff-test", now);
        await dbContext.IncorporatedProperties.AddAsync(incorporatedProperty);

        var offer = CommercialOffer.Create(propertyId, "staff-test", now);
        await dbContext.CommercialOffers.AddAsync(offer);
        await dbContext.SaveChangesAsync();

        var snapshotJson = JsonSerializer.Serialize(new { accommodations = 3, policies = 2 });
        var validation = OfferValidation.Create(
            Guid.NewGuid(),
            propertyId,
            1,
            "reviewer-test",
            now);
        await dbContext.OfferValidations.AddAsync(validation);
        var submission = OfferSubmission.Create(
            Guid.NewGuid(),
            propertyId,
            1,
            validation.Id,
            snapshotJson,
            "staff-test",
            now);

        await dbContext.OfferSubmissions.AddAsync(submission);
        await dbContext.SaveChangesAsync();
        dbContext.ChangeTracker.Clear();

        var loaded = await dbContext.OfferSubmissions
            .AsNoTracking()
            .SingleOrDefaultAsync(s => s.Id == submission.Id);

        loaded.Should().NotBeNull();
        var persistedSnapshot = JsonSerializer.Deserialize<JsonElement>(loaded!.SnapshotJson);
        persistedSnapshot.GetProperty("accommodations").GetInt32().Should().Be(3);
        persistedSnapshot.GetProperty("policies").GetInt32().Should().Be(2);
        loaded.PropertyId.Should().Be(propertyId);
        loaded.ValidationId.Should().Be(validation.Id);
    }

    [Fact]
    public async Task CommercialOfferIdempotencyKey_DuplicateComposite_FailsOnUniqueIndex()
    {
        using var scope = _factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<InventoryDbContext>();
        await dbContext.Database.MigrateAsync();
        await ClearCommercialOfferDataAsync(dbContext);

        var now = DateTimeOffset.UtcNow;
        var propertyId = Guid.NewGuid();

        var incorporatedProperty = IncorporatedProperty.Create(
            propertyId, Guid.NewGuid(), "Hotel Test", "dest-001", "staff-test", now);
        await dbContext.IncorporatedProperties.AddAsync(incorporatedProperty);

        var offer = CommercialOffer.Create(propertyId, "staff-test", now);
        await dbContext.CommercialOffers.AddAsync(offer);
        await dbContext.SaveChangesAsync();

        var key = Guid.NewGuid();
        var scopeVal = "submission";
        var first = CommercialOfferIdempotencyKey.Create(propertyId, key, scopeVal, now, "fp-001");
        var second = CommercialOfferIdempotencyKey.Create(propertyId, key, scopeVal, now.AddSeconds(1), "fp-002");

        await dbContext.CommercialOfferIdempotencyKeys.AddAsync(first);
        await dbContext.SaveChangesAsync();
        dbContext.ChangeTracker.Clear();

        await dbContext.CommercialOfferIdempotencyKeys.AddAsync(second);
        var action = () => dbContext.SaveChangesAsync();

        await action.Should().ThrowAsync<DbUpdateException>();
    }

    [Fact]
    public async Task CommercialPolicy_CascadeDelete_WithOffer_RemovesChildren()
    {
        using var scope = _factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<InventoryDbContext>();
        await dbContext.Database.MigrateAsync();
        await ClearCommercialOfferDataAsync(dbContext);

        var now = DateTimeOffset.UtcNow;
        var propertyId = Guid.NewGuid();

        var incorporatedProperty = IncorporatedProperty.Create(
            propertyId, Guid.NewGuid(), "Hotel Test", "dest-001", "staff-test", now);
        await dbContext.IncorporatedProperties.AddAsync(incorporatedProperty);

        var offer = CommercialOffer.Create(propertyId, "staff-test", now);
        await dbContext.CommercialOffers.AddAsync(offer);
        await dbContext.SaveChangesAsync();

        var ruleSet = new LocalizeStay.Modules.Inventory.Application.LegalPolicies.CommercialPolicyRuleSet(
            LocalizeStay.Modules.Inventory.Application.LegalPolicies.PolicyType.Flexible,
            "Flexible Policy",
            "Free cancellation up to 24h before check-in.",
            "v1.0");

        var policy = CommercialPolicy.Create(Guid.NewGuid(), propertyId, ruleSet, true, now);
        await dbContext.CommercialPolicies.AddAsync(policy);
        await dbContext.SaveChangesAsync();
        dbContext.ChangeTracker.Clear();

        var policyCountBefore = await dbContext.CommercialPolicies.CountAsync(p => p.PropertyId == propertyId);
        policyCountBefore.Should().Be(1);

        dbContext.IncorporatedProperties.Remove(incorporatedProperty);
        await dbContext.SaveChangesAsync();

        var policyCountAfter = await dbContext.CommercialPolicies
            .IgnoreQueryFilters()
            .CountAsync(p => p.PropertyId == propertyId);
        policyCountAfter.Should().Be(0);
    }

    [Fact]
    public async Task OfferValidation_Index_CoversPropertyIdAndRevision()
    {
        using var scope = _factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<InventoryDbContext>();
        await dbContext.Database.MigrateAsync();
        await ClearCommercialOfferDataAsync(dbContext);

        var now = DateTimeOffset.UtcNow;
        var propertyId = Guid.NewGuid();

        var incorporatedProperty = IncorporatedProperty.Create(
            propertyId, Guid.NewGuid(), "Hotel Test", "dest-001", "staff-test", now);
        await dbContext.IncorporatedProperties.AddAsync(incorporatedProperty);

        var offer = CommercialOffer.Create(propertyId, "staff-test", now);
        await dbContext.CommercialOffers.AddAsync(offer);
        await dbContext.SaveChangesAsync();

        var validation = OfferValidation.Create(Guid.NewGuid(), propertyId, 1, "reviewer-test", now);
        await dbContext.OfferValidations.AddAsync(validation);
        await dbContext.SaveChangesAsync();
        dbContext.ChangeTracker.Clear();

        var loaded = await dbContext.OfferValidations
            .AsNoTracking()
            .SingleOrDefaultAsync(v => v.PropertyId == propertyId && v.Revision == 1);

        loaded.Should().NotBeNull();
        loaded!.Status.Should().Be(ValidationStatus.Valid);
        loaded.ValidatedBy.Should().Be("reviewer-test");
    }

    [Fact]
    public async Task Backfill_IncorporatedProperties_IsIdempotent()
    {
        using var scope = _factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<InventoryDbContext>();
        await dbContext.Database.MigrateAsync();
        await ClearCommercialOfferDataAsync(dbContext);

        var now = DateTimeOffset.UtcNow;
        var propertyId = Guid.NewGuid();

        var incorporatedProperty = IncorporatedProperty.Create(
            propertyId, Guid.NewGuid(), "Hotel Test", "dest-001", "staff-test", now);
        await dbContext.IncorporatedProperties.AddAsync(incorporatedProperty);
        await dbContext.SaveChangesAsync();
        dbContext.ChangeTracker.Clear();

        var existingCount = await dbContext.IncorporatedProperties
            .CountAsync(ip => ip.Id == propertyId);
        existingCount.Should().Be(1);
    }

    private static async Task ClearCommercialOfferDataAsync(InventoryDbContext dbContext)
    {
        await dbContext.Database.ExecuteSqlRawAsync(
            """
            TRUNCATE TABLE
              inventory.commercial_offer_idempotency_keys,
              inventory.commercial_rates,
              inventory.accommodations,
              inventory.commercial_policies,
              inventory.offer_returns,
              inventory.offer_submissions,
              inventory.offer_validations,
              inventory.commercial_offers,
              inventory.incorporated_properties
            CASCADE;
            """);
    }
}
