using AwesomeAssertions;
using LocalizeStay.Modules.Inventory.Infrastructure;
using LocalizeStay.SharedKernel.Auditing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace LocalizeStay.UnitTests.Inventory;

/// <summary>
/// AAA unit tests for the shared business audit contract (task 2.0). Uses the real
/// <see cref="InventoryDbContext"/> with the EF Core InMemory provider so we verify the actual
/// wiring between the closed generic <see cref="BusinessAuditWriter{TDbContext}"/> and the
/// Inventory schema owner.
/// </summary>
public class BusinessAuditWriterTests
{
    private static BusinessAuditEntry BuildEntry(
        string? metadataKey = null,
        string? metadataValue = null)
    {
        var metadata = metadataKey is null && metadataValue is null
            ? null
            : new Dictionary<string, string> { [metadataKey!] = metadataValue! };

        return BusinessAuditEntry.Create(
            aggregateType: "PropertyOnboarding",
            aggregateId: "por_0123456789",
            actor: "user_staff_42",
            auditType: "ReadinessGateCompleted",
            summary: "Gate 'legal' marked completed by operator.",
            occurredOnUtc: DateTimeOffset.Parse("2026-07-19T10:00:00Z"),
            correlationId: "corr-abc-123",
            metadata);
    }

    private static InventoryDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<InventoryDbContext>()
            .UseInMemoryDatabase($"audit-{Guid.NewGuid():N}")
            .Options;
        return new InventoryDbContext(options);
    }

    [Fact]
    public void Create_should_materialize_immutable_entry_with_required_fields()
    {
        var entry = BuildEntry();

        entry.Id.Should().NotBeEmpty();
        entry.AggregateType.Should().Be("PropertyOnboarding");
        entry.AggregateId.Should().Be("por_0123456789");
        entry.Actor.Should().Be("user_staff_42");
        entry.AuditType.Should().Be("ReadinessGateCompleted");
        entry.Summary.Should().Be("Gate 'legal' marked completed by operator.");
        entry.CorrelationId.Should().Be("corr-abc-123");
        entry.OccurredOnUtc.Should().Be(DateTimeOffset.Parse("2026-07-19T10:00:00Z"));
        entry.Metadata.Should().BeEmpty();
    }

    [Theory]
    [InlineData("", "por_1", "actor", "type", "summary", "corr")]
    [InlineData("PropertyOnboarding", "", "actor", "type", "summary", "corr")]
    [InlineData("PropertyOnboarding", "por_1", "", "type", "summary", "corr")]
    [InlineData("PropertyOnboarding", "por_1", "actor", "", "summary", "corr")]
    [InlineData("PropertyOnboarding", "por_1", "actor", "type", "", "corr")]
    [InlineData("PropertyOnboarding", "por_1", "actor", "type", "summary", "")]
    public void Create_should_throw_when_a_required_field_is_empty(
        string aggregateType, string aggregateId, string actor, string auditType, string summary, string correlationId)
    {
        var act = () => BusinessAuditEntry.Create(
            aggregateType, aggregateId, actor, auditType, summary,
            DateTimeOffset.UtcNow, correlationId);

        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Create_should_reject_summary_longer_than_limit()
    {
        var tooLongSummary = new string('a', 501);

        var act = () => BusinessAuditEntry.Create(
            "PropertyOnboarding", "por_1", "actor", "type", tooLongSummary,
            DateTimeOffset.UtcNow, "corr");

        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Create_should_reject_metadata_with_unapproved_key()
    {
        var metadata = new Dictionary<string, string> { ["contractDocumentUrl"] = "ref-1" };

        var act = () => BusinessAuditEntry.Create(
            "PropertyOnboarding", "por_1", "actor", "type", "summary",
            DateTimeOffset.UtcNow, "corr", metadata);

        act.Should().Throw<ArgumentException>(
            "metadata must be limited to a whitelist of non-PII business keys (ADR-003 / LGPD).");
    }

    [Theory]
    [InlineData("partnerId", "user@example.com", "email")]
    [InlineData("partnerId", "123.456.789-09", "CPF")]
    [InlineData("partnerId", "11.222.333/0001-81", "CNPJ")]
    public void Create_should_reject_metadata_value_that_looks_like_personal_data(string key, string value, string label)
    {
        var metadata = new Dictionary<string, string> { [key] = value };

        var act = () => BusinessAuditEntry.Create(
            "PropertyOnboarding", "por_1", "actor", "type", "summary",
            DateTimeOffset.UtcNow, "corr", metadata);

        act.Should().Throw<ArgumentException>($"audit metadata must not contain {label}-shaped data.");
    }

    [Fact]
    public void Create_should_accept_approved_metadata_without_pii()
    {
        var metadata = new Dictionary<string, string>
        {
            ["partnerId"] = "par_42",
            ["gateType"] = "legal",
            ["idempotencyKey"] = "idem-1",
        };

        var entry = BusinessAuditEntry.Create(
            "PropertyOnboarding", "por_1", "actor", "type", "summary",
            DateTimeOffset.UtcNow, "corr", metadata);

        entry.Metadata.Should().HaveCount(3);
        entry.Metadata["partnerId"].Should().Be("par_42");
        entry.Metadata["gateType"].Should().Be("legal");
        entry.Metadata["idempotencyKey"].Should().Be("idem-1");
    }

    [Fact]
    public void Create_should_freeze_metadata_so_external_mutation_has_no_effect()
    {
        var source = new Dictionary<string, string> { ["partnerId"] = "par_1" };
        var entry = BusinessAuditEntry.Create(
            "PropertyOnboarding", "por_1", "actor", "type", "summary",
            DateTimeOffset.UtcNow, "corr", source);

        source["partnerId"] = "par_999";
        source["gateType"] = "legal";

        entry.Metadata["partnerId"].Should().Be("par_1");
        entry.Metadata.Should().HaveCount(1);
    }

    [Fact]
    public async Task Record_should_attach_exactly_one_entry_without_calling_SaveChanges()
    {
        await using var context = CreateContext();
        var writer = new BusinessAuditWriter<InventoryDbContext>(context);
        var entry = BuildEntry();

        writer.Record(entry);

        // Tracked locally for the next SaveChangesAsync...
        context.ChangeTracker.Entries<BusinessAuditEntry>().Should().ContainSingle(
            "the writer must stage the audit row without committing on its own (ADR-003).");
        context.ChangeTracker.Entries<BusinessAuditEntry>().Single().Entity.Should().BeSameAs(entry);

        // ...but nothing was actually persisted, because Record must not call SaveChanges.
        var persisted = await context.BusinessAuditEntries.ToListAsync();
        persisted.Should().BeEmpty(
            "Record must not call SaveChangesAsync; the handler owns the transaction so rollback removes both the business change and the audit row.");
    }

    [Fact]
    public async Task Record_should_let_savechanges_round_trip_the_entry_in_the_same_transaction()
    {
        await using var context = CreateContext();
        var writer = new BusinessAuditWriter<InventoryDbContext>(context);
        var entry = BuildEntry();

        writer.Record(entry);
        await context.SaveChangesAsync();

        var persisted = await context.BusinessAuditEntries.SingleAsync();
        persisted.AggregateType.Should().Be(entry.AggregateType);
        persisted.AggregateId.Should().Be(entry.AggregateId);
        persisted.CorrelationId.Should().Be(entry.CorrelationId);
    }

    [Fact]
    public void Record_should_throw_when_entry_is_null()
    {
        using var context = CreateContext();
        var writer = new BusinessAuditWriter<InventoryDbContext>(context);

        var act = () => writer.Record(null!);

        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void AddBusinessAuditWriter_should_register_scoped_writer_for_the_owning_dbcontext()
    {
        var services = new ServiceCollection();
        services.AddDbContext<InventoryDbContext>(options => options.UseInMemoryDatabase($"audit-di-{Guid.NewGuid():N}"));
        services.AddBusinessAuditWriter<InventoryDbContext>();

        using var provider = services.BuildServiceProvider(validateScopes: true);
        using var scope = provider.CreateScope();

        var writer = scope.ServiceProvider.GetRequiredService<IBusinessAuditWriter>();
        writer.Should().BeOfType<BusinessAuditWriter<InventoryDbContext>>();
    }
}
