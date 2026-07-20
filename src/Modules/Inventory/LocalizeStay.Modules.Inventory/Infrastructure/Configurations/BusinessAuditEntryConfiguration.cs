using System.Text.Json;
using LocalizeStay.SharedKernel.Auditing;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace LocalizeStay.Modules.Inventory.Infrastructure.Configurations;

internal sealed class BusinessAuditEntryConfiguration : IEntityTypeConfiguration<BusinessAuditEntry>
{
    private static readonly JsonSerializerOptions _metadataSerializerOptions = new(JsonSerializerDefaults.Web);

    public void Configure(EntityTypeBuilder<BusinessAuditEntry> builder)
    {
        builder.ToTable("audit_entries", InventoryDbContext.SchemaName);

        builder.HasKey(entry => entry.Id);
        builder.Property(entry => entry.Id).ValueGeneratedNever();

        builder.Property(entry => entry.AggregateType).HasMaxLength(100).IsRequired();
        builder.Property(entry => entry.AggregateId).HasMaxLength(100).IsRequired();
        builder.Property(entry => entry.Actor).HasMaxLength(200).IsRequired();
        builder.Property(entry => entry.AuditType).HasMaxLength(100).IsRequired();
        builder.Property(entry => entry.Summary).HasMaxLength(500).IsRequired();
        builder.Property(entry => entry.CorrelationId).HasMaxLength(100).IsRequired();
        builder.Property(entry => entry.OccurredOnUtc).IsRequired();

        builder.Property(entry => entry.Metadata)
            .HasColumnType("jsonb")
            .HasConversion(
                metadata => JsonSerializer.Serialize(metadata, _metadataSerializerOptions),
                json => JsonSerializer.Deserialize<Dictionary<string, string>>(json, _metadataSerializerOptions)
                    ?? new Dictionary<string, string>())
            .Metadata
            .SetValueComparer(new ValueComparer<IReadOnlyDictionary<string, string>>(
                (left, right) => left!.SequenceEqual(right!),
                dictionary => dictionary.Count,
                dictionary => new Dictionary<string, string>(dictionary)));

        // Append-only history is mostly read by aggregate, then by correlation.
        builder.HasIndex(entry => new { entry.AggregateType, entry.AggregateId, entry.OccurredOnUtc })
            .HasDatabaseName("ix_audit_entries_aggregate_occurred");

        builder.HasIndex(entry => entry.CorrelationId)
            .HasDatabaseName("ix_audit_entries_correlation_id");
    }
}
