using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using AwesomeAssertions;
using LocalizeStay.IntegrationTests.Infrastructure;
using LocalizeStay.Modules.Inventory.Domain.Partners;
using LocalizeStay.Modules.Inventory.Domain.PropertyOnboardings;
using LocalizeStay.Modules.Inventory.Infrastructure;
using LocalizeStay.SharedKernel.Auditing;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.Extensions.DependencyInjection;

namespace LocalizeStay.IntegrationTests.Inventory;

/// <summary>
/// Persistence-level coverage for the F01 portfolio onboarding model: EF mappings, migration apply
/// and revert, unique constraints, partial indexes and the shared business audit table in the
/// <c>inventory</c> PostgreSQL schema (dotnet-testing baseline: real Testcontainers PostgreSQL).
/// </summary>
public sealed class InventoryPersistenceTests : IClassFixture<LocalizeStayWebApplicationFactory>
{
    private readonly LocalizeStayWebApplicationFactory _factory;

    public InventoryPersistenceTests(LocalizeStayWebApplicationFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task Migration_AppliesAndReadyHealthCheckReturns200()
    {
        // Arrange
        using var scope = _factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<InventoryDbContext>();
        await dbContext.Database.MigrateAsync();

        // Act
        var canConnect = await dbContext.Database.CanConnectAsync();

        // Assert
        canConnect.Should().BeTrue();

        var client = _factory.CreateClient();
        var response = await client.GetAsync("/health/ready");
        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Migration_CanBeReverted()
    {
        // Arrange
        using var scope = _factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<InventoryDbContext>();
        try
        {
            // Start from a genuinely clean database, rather than merely checking a migration that
            // another test might already have applied through the shared factory.
            await dbContext.Database.GetDbConnection().OpenAsync();
            await dbContext.Database.GetService<IMigrator>().MigrateAsync("0");

            (await dbContext.Database.GetAppliedMigrationsAsync()).Should().BeEmpty();
            var tables = (await GetInventoryTableNamesAsync(dbContext))
                .Where(name => name != "__ef_migrations_history")
                .ToList();
            tables.Should().BeEmpty();

            // Apply the full Inventory chain to the clean database.
            await dbContext.Database.MigrateAsync();

            var appliedMigrations = await dbContext.Database.GetAppliedMigrationsAsync();
            appliedMigrations.Should().BeEquivalentTo(
                dbContext.Database.GetMigrations(),
                options => options.WithStrictOrdering());
            (await GetInventoryTableNamesAsync(dbContext)).Should().Contain("partners");
        }
        finally
        {
            // Re-apply so later tests in the shared fixture continue to see the schema.
            await dbContext.Database.MigrateAsync();
        }
    }

    [Fact]
    public async Task Partner_WithDuplicateLegalIdentifier_FailsOnUniqueIndex()
    {
        // Arrange
        using var scope = _factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<InventoryDbContext>();
        await dbContext.Database.MigrateAsync();
        await ClearInventoryDataAsync(dbContext);

        var now = DateTimeOffset.UtcNow;
        var firstPartner = Partner.Create(
            Guid.NewGuid(),
            "pre-001",
            "Hotel One",
            null,
            new LegalIdentifier(LegalIdentifierType.Cnpj, "BR", "12.345.678/0001-90"),
            new Contact("John Doe", "john@hotelone.com", "+55 11 91234-5678"),
            now);

        var secondPartner = Partner.Create(
            Guid.NewGuid(),
            "pre-002",
            "Hotel Two",
            null,
            new LegalIdentifier(LegalIdentifierType.Cnpj, "BR", "12345678000190"),
            new Contact("Jane Doe", "jane@hoteltwo.com", "+55 11 98765-4321"),
            now);

        await dbContext.Partners.AddAsync(firstPartner);
        await dbContext.SaveChangesAsync();
        dbContext.ChangeTracker.Clear();

        // Act
        await dbContext.Partners.AddAsync(secondPartner);
        var action = () => dbContext.SaveChangesAsync();

        // Assert
        await action.Should().ThrowAsync<DbUpdateException>();
    }

    [Fact]
    public async Task PropertyOnboarding_WithActiveSimilarityKey_FailsOnPartialUniqueIndex()
    {
        // Arrange
        using var scope = _factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<InventoryDbContext>();
        await dbContext.Database.MigrateAsync();
        await ClearInventoryDataAsync(dbContext);

        var partner = await SeedPartnerAsync(dbContext);
        var destinationId = $"dest-{Guid.NewGuid():N}";
        var address = new Address("Rua Augusta", "100", null, "Consolação", "São Paulo", "SP", "01305-100", "BR");
        var first = PropertyOnboarding.Create(
            Guid.NewGuid(),
            partner.Id,
            "pre-001",
            new Property("Hotel Augusta", destinationId, address),
            DateTimeOffset.UtcNow,
            TimeSpan.FromDays(10));

        var second = PropertyOnboarding.Create(
            Guid.NewGuid(),
            partner.Id,
            "pre-002",
            new Property("Hotel Augusta II", destinationId, address),
            DateTimeOffset.UtcNow,
            TimeSpan.FromDays(10));

        await dbContext.PropertyOnboardings.AddAsync(first);
        await dbContext.SaveChangesAsync();
        dbContext.ChangeTracker.Clear();

        // Act
        await dbContext.PropertyOnboardings.AddAsync(second);
        var action = () => dbContext.SaveChangesAsync();

        // Assert
        await action.Should().ThrowAsync<DbUpdateException>();
    }

    [Fact]
    public async Task PropertyOnboarding_ClosedThenReopened_WithSameSimilarityKey_Succeeds()
    {
        // Arrange
        using var scope = _factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<InventoryDbContext>();
        await dbContext.Database.MigrateAsync();
        await ClearInventoryDataAsync(dbContext);

        var partner = await SeedPartnerAsync(dbContext);
        var destinationId = $"dest-{Guid.NewGuid():N}";
        var address = new Address("Rua Augusta", "100", null, "Consolação", "São Paulo", "SP", "01305-100", "BR");
        var first = PropertyOnboarding.Create(
            Guid.NewGuid(),
            partner.Id,
            "pre-001",
            new Property("Hotel Augusta", destinationId, address),
            DateTimeOffset.UtcNow,
            TimeSpan.FromDays(10));

        first.Close(CloseReasonCode.PartnerWithdrawal, "Partner withdrew after initial contact.", DateTimeOffset.UtcNow, "staff-001");

        var second = PropertyOnboarding.Create(
            Guid.NewGuid(),
            partner.Id,
            "pre-002",
            new Property("Hotel Augusta", destinationId, address),
            DateTimeOffset.UtcNow,
            TimeSpan.FromDays(10));

        await dbContext.PropertyOnboardings.AddAsync(first);
        await dbContext.SaveChangesAsync();
        dbContext.ChangeTracker.Clear();

        // Act
        await dbContext.PropertyOnboardings.AddAsync(second);
        var action = () => dbContext.SaveChangesAsync();

        // Assert
        await action.Should().NotThrowAsync();
    }

    [Fact]
    public async Task BusinessAuditEntry_CanBeInsertedWithMetadata()
    {
        // Arrange
        using var scope = _factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<InventoryDbContext>();
        await dbContext.Database.MigrateAsync();
        await ClearInventoryDataAsync(dbContext);

        var entry = BusinessAuditEntry.Create(
            "PropertyOnboarding",
            Guid.NewGuid().ToString(),
            "staff-001",
            "PropertyOnboardingOpened",
            "Onboarding opened for partner.",
            DateTimeOffset.UtcNow,
            "corr-001",
            new Dictionary<string, string>
            {
                ["partnerId"] = Guid.NewGuid().ToString(),
                ["propertyOnboardingId"] = Guid.NewGuid().ToString(),
            });

        // Act
        await dbContext.BusinessAuditEntries.AddAsync(entry);
        await dbContext.SaveChangesAsync();
        dbContext.ChangeTracker.Clear();

        // Assert
        var loaded = await dbContext.BusinessAuditEntries
            .AsNoTracking()
            .SingleAsync(e => e.Id == entry.Id);

        loaded.Metadata.Should().ContainKey("partnerId");
        loaded.Metadata.Should().ContainKey("propertyOnboardingId");
    }

    [Fact]
    public async Task IdempotencyKey_WithDuplicateComposite_FailsOnUniqueIndex()
    {
        // Arrange
        using var scope = _factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<InventoryDbContext>();
        await dbContext.Database.MigrateAsync();
        await ClearInventoryDataAsync(dbContext);

        var partner = await SeedPartnerAsync(dbContext);
        var destinationId = $"dest-{Guid.NewGuid():N}";
        var onboarding = PropertyOnboarding.Create(
            Guid.NewGuid(),
            partner.Id,
            "pre-001",
            new Property("Hotel Augusta", destinationId, new Address("Rua Augusta", "100", null, "Consolação", "São Paulo", "SP", "01305-100", "BR")),
            DateTimeOffset.UtcNow,
            TimeSpan.FromDays(10));

        var key = Guid.NewGuid();
        var first = IdempotencyKey.Create(onboarding.Id, key, IdempotencyScope.SubmitToCuration, DateTimeOffset.UtcNow);
        var second = IdempotencyKey.Create(onboarding.Id, key, IdempotencyScope.SubmitToCuration, DateTimeOffset.UtcNow.AddSeconds(1));

        await dbContext.PropertyOnboardings.AddAsync(onboarding);
        await dbContext.IdempotencyKeys.AddAsync(first);
        await dbContext.SaveChangesAsync();
        dbContext.ChangeTracker.Clear();

        // Act
        await dbContext.IdempotencyKeys.AddAsync(second);
        var action = () => dbContext.SaveChangesAsync();

        // Assert
        await action.Should().ThrowAsync<DbUpdateException>();
    }

    [Fact]
    public async Task ReadinessGate_WithEvidence_PersistsAsJson()
    {
        // Arrange
        using var scope = _factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<InventoryDbContext>();
        await dbContext.Database.MigrateAsync();
        await ClearInventoryDataAsync(dbContext);

        var partner = await SeedPartnerAsync(dbContext);
        var destinationId = $"dest-{Guid.NewGuid():N}";
        var onboarding = PropertyOnboarding.Create(
            Guid.NewGuid(),
            partner.Id,
            "pre-001",
            new Property("Hotel Augusta", destinationId, new Address("Rua Augusta", "100", null, "Consolação", "São Paulo", "SP", "01305-100", "BR")),
            DateTimeOffset.UtcNow,
            TimeSpan.FromDays(10));

        var contractEvidence = new EvidenceReference(EvidenceKind.Contract, "contract-001", "Signed contract");
        onboarding.ValidateGate(
            ReadinessGateType.SignedContract,
            [contractEvidence],
            "staff-001",
            DateTimeOffset.UtcNow);

        // Act
        await dbContext.PropertyOnboardings.AddAsync(onboarding);
        await dbContext.SaveChangesAsync();
        dbContext.ChangeTracker.Clear();

        // Assert
        var loaded = await dbContext.PropertyOnboardings
            .AsNoTracking()
            .Include(po => po.ReadinessGates)
            .SingleAsync(po => po.Id == onboarding.Id);

        var signedContractGate = loaded.ReadinessGates.Single(g => g.Type == ReadinessGateType.SignedContract);
        signedContractGate.Status.Should().Be(ReadinessGateStatus.Validated);
        signedContractGate.Evidence.Should().ContainSingle()
            .Which.Reference.Should().Be("contract-001");
    }

    private static async Task<Partner> SeedPartnerAsync(InventoryDbContext dbContext)
    {
        var partner = Partner.Create(
            Guid.NewGuid(),
            $"pre-{Guid.NewGuid():N}",
            "Hotel Augusta",
            null,
            new LegalIdentifier(LegalIdentifierType.Cnpj, "BR", $"{Guid.NewGuid():N}"[..14]),
            new Contact("John Doe", $"john{Guid.NewGuid():N}@hotel.com", "+55 11 91234-5678"),
            DateTimeOffset.UtcNow);

        await dbContext.Partners.AddAsync(partner);
        await dbContext.SaveChangesAsync();
        dbContext.ChangeTracker.Clear();

        return partner;
    }

    private static async Task ClearInventoryDataAsync(InventoryDbContext dbContext)
    {
        await dbContext.Database.ExecuteSqlRawAsync(
            """
            TRUNCATE TABLE
              inventory.idempotency_keys,
              inventory.curation_returns,
              inventory.duplicate_reviews,
              inventory.communication_records,
              inventory.pending_issues,
              inventory.readiness_gates,
              inventory.property_onboardings,
              inventory.partners,
              inventory.audit_entries,
              inventory.outbox_messages
            CASCADE;
            """);
    }

    private static async Task<IReadOnlyList<string>> GetInventoryTableNamesAsync(InventoryDbContext dbContext)
    {
        await using var command = dbContext.Database.GetDbConnection().CreateCommand();
        command.CommandText =
            "SELECT table_name FROM information_schema.tables WHERE table_schema = 'inventory';";

        var tables = new List<string>();
        await using var reader = await command.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            tables.Add(reader.GetString(0));
        }

        return tables;
    }
}
