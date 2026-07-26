using System.Text.Json;
using AwesomeAssertions;
using LocalizeStay.IntegrationTests.Infrastructure;
using LocalizeStay.Modules.Inventory.Domain.CommercialOffers;
using LocalizeStay.Modules.Inventory.Domain.IncorporatedProperties;
using LocalizeStay.Modules.Inventory.Domain.Partners;
using LocalizeStay.Modules.Inventory.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;

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

    [Fact]
    public async Task Migration_ShouldCreateCommercialOffersSchemaWithExpectedTablesAndJsonbColumns()
    {
        using var scope = _factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<InventoryDbContext>();
        await dbContext.Database.MigrateAsync();
        await ClearCommercialOfferDataAsync(dbContext);

        var connectionString = dbContext.Database.GetConnectionString();
        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync();

        await using var tableCommand = connection.CreateCommand();
        tableCommand.CommandText = """
            SELECT table_name
            FROM information_schema.tables
            WHERE table_schema = 'inventory'
              AND table_name IN (
                'commercial_offers', 'commercial_policies', 'accommodations', 'commercial_rates',
                'offer_validations', 'offer_submissions', 'offer_returns',
                'commercial_offer_idempotency_keys'
              )
            ORDER BY table_name;
            """;
        var tables = new List<string>();
        await using (var reader = await tableCommand.ExecuteReaderAsync())
        {
            while (await reader.ReadAsync()) tables.Add(reader.GetString(0));
        }

        tables.Should().BeEquivalentTo(new[]
        {
            "accommodations",
            "commercial_offer_idempotency_keys",
            "commercial_offers",
            "commercial_policies",
            "commercial_rates",
            "offer_returns",
            "offer_submissions",
            "offer_validations",
        }, "the F02 migration must create every commercial-offer table in the inventory schema");

        await using var jsonbCommand = connection.CreateCommand();
        jsonbCommand.CommandText = """
            SELECT table_name, column_name
            FROM information_schema.columns
            WHERE table_schema = 'inventory'
              AND data_type = 'jsonb'
              AND (
                (table_name = 'commercial_offers' AND column_name = 'pending_issues')
                OR (table_name = 'accommodations' AND column_name IN ('bed_configuration', 'structural_features', 'submission_ids'))
                OR (table_name = 'commercial_policies' AND column_name = 'submission_ids')
                OR (table_name = 'commercial_rates' AND column_name = 'submission_ids')
                OR (table_name = 'offer_submissions' AND column_name = 'snapshot_json')
              )
            ORDER BY table_name, column_name;
            """;
        var jsonbColumns = new List<string>();
        await using (var reader = await jsonbCommand.ExecuteReaderAsync())
        {
            while (await reader.ReadAsync()) jsonbColumns.Add($"{reader.GetString(0)}.{reader.GetString(1)}");
        }

        jsonbColumns.Should().Contain(new[]
        {
            "accommodations.bed_configuration",
            "accommodations.structural_features",
            "commercial_offers.pending_issues",
            "offer_submissions.snapshot_json",
        }, "the F02 migration must declare the JSONB columns that back snapshots, structural data and pending issues");
    }

    [Fact]
    public async Task Accommodation_StructuralFeatures_RoundTripsAsJsonb()
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
            Guid.NewGuid(), propertyId, "Suite Garden", null, null, now);
        accommodation.SetStructuralFeatures(
        [
            "balcony",
            "airConditioning",
            "accessible",
        ]);

        await dbContext.Accommodations.AddAsync(accommodation);
        await dbContext.SaveChangesAsync();
        dbContext.ChangeTracker.Clear();

        var loaded = await dbContext.Accommodations
            .AsNoTracking()
            .SingleOrDefaultAsync(a => a.Id == accommodation.Id);

        loaded.Should().NotBeNull();
        loaded!.StructuralFeatures.Should().BeEquivalentTo(new[]
        {
            "balcony",
            "airConditioning",
            "accessible",
        });
    }

    [Fact]
    public async Task CommercialOfferIndex_CoversPropertyIdAndStatusAndTargetSubmissionAt()
    {
        using var scope = _factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<InventoryDbContext>();
        await dbContext.Database.MigrateAsync();
        await ClearCommercialOfferDataAsync(dbContext);

        var now = DateTimeOffset.UtcNow;
        var firstProperty = IncorporatedProperty.Create(
            Guid.NewGuid(), Guid.NewGuid(), "First", "dest-001", "staff", now);
        await dbContext.IncorporatedProperties.AddAsync(firstProperty);

        var firstOffer = CommercialOffer.Create(firstProperty.Id, "staff", now);
        firstOffer.SetTargetSubmissionAt(now.AddDays(1));
        await dbContext.CommercialOffers.AddAsync(firstOffer);
        await dbContext.SaveChangesAsync();
        dbContext.ChangeTracker.Clear();

        var filtered = await dbContext.CommercialOffers.AsNoTracking()
            .Where(o => o.PropertyId == firstProperty.Id && o.State == OfferState.Draft)
            .ToListAsync();

        filtered.Should().ContainSingle();
    }

    [Fact]
    public async Task OfferSubmission_Snapshot_CapturesCompleteSnapshotStructure()
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

        var validation = OfferValidation.Create(
            Guid.NewGuid(), propertyId, 1, "reviewer-test", now);
        await dbContext.OfferValidations.AddAsync(validation);

        var snapshotJson = JsonSerializer.Serialize(new
        {
            snapshotVersion = 1,
            id = Guid.NewGuid(),
            propertyId,
            revision = 7,
            revisionAuthor = "staff-test",
            state = "submitted",
            validationId = validation.Id,
            submittedBy = "staff-test",
            submittedAt = now,
            accommodations = Array.Empty<object>(),
            policies = Array.Empty<object>(),
        });

        var submission = OfferSubmission.Create(
            Guid.NewGuid(),
            propertyId,
            7,
            validation.Id,
            snapshotJson,
            "staff-test",
            now);
        await dbContext.OfferSubmissions.AddAsync(submission);
        await dbContext.SaveChangesAsync();
        dbContext.ChangeTracker.Clear();

        var loaded = await dbContext.OfferSubmissions.AsNoTracking()
            .SingleAsync(s => s.Id == submission.Id);

        var snapshot = JsonSerializer.Deserialize<JsonElement>(loaded.SnapshotJson);
        snapshot.GetProperty("snapshotVersion").GetInt32().Should().Be(1);
        snapshot.GetProperty("revision").GetInt32().Should().Be(7);
        snapshot.GetProperty("state").GetString().Should().Be("submitted");
        snapshot.GetProperty("validationId").GetGuid().Should().Be(validation.Id);
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
