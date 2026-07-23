using System.Text.Json;
using LocalizeStay.Modules.Inventory.Domain.CommercialOffers;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace LocalizeStay.Modules.Inventory.Infrastructure.Configurations;

internal sealed class CommercialPolicyConfiguration : IEntityTypeConfiguration<CommercialPolicy>
{
    private static readonly JsonSerializerOptions _jsonSerializerOptions = new(JsonSerializerDefaults.Web);

    public void Configure(EntityTypeBuilder<CommercialPolicy> builder)
    {
        builder.ToTable("commercial_policies", InventoryDbContext.SchemaName);

        builder.HasKey(p => p.Id);
        builder.Property(p => p.Id).ValueGeneratedNever();

        builder.Property(p => p.PropertyId)
            .HasColumnName("property_id")
            .IsRequired();

        builder.Property(p => p.Type)
            .HasColumnName("type")
            .HasConversion<string>()
            .HasMaxLength(50)
            .IsRequired();

        builder.Property(p => p.Status)
            .HasColumnName("status")
            .HasConversion<string>()
            .HasMaxLength(50)
            .IsRequired();

        builder.Property(p => p.IsDefault)
            .HasColumnName("is_default")
            .IsRequired();

        builder.Property(p => p.UsageCount)
            .HasColumnName("usage_count")
            .IsRequired();

        builder.Property(p => p.EverSubmitted)
            .HasColumnName("ever_submitted")
            .IsRequired();

        builder.Property(p => p.Title)
            .HasColumnName("title")
            .HasMaxLength(300)
            .IsRequired();

        builder.Property(p => p.RulesSummary)
            .HasColumnName("rules_summary")
            .HasMaxLength(2000)
            .IsRequired();

        builder.Property(p => p.RuleSetVersion)
            .HasColumnName("rule_set_version")
            .HasMaxLength(100)
            .IsRequired();

        builder.Property(p => p.CreatedAt)
            .HasColumnName("created_at")
            .IsRequired();

        builder.Property(p => p.UpdatedAt)
            .HasColumnName("updated_at")
            .IsRequired();

        builder.Property<List<Guid>>("_submissionIds")
            .HasColumnName("submission_ids")
            .HasColumnType("jsonb")
            .HasConversion(
                v => JsonSerializer.Serialize(v, _jsonSerializerOptions),
                json => JsonSerializer.Deserialize<List<Guid>>(json, _jsonSerializerOptions) ?? new List<Guid>())
            .Metadata
            .SetValueComparer(new ValueComparer<List<Guid>>(
                (left, right) => (left ?? new List<Guid>()).SequenceEqual(right ?? new List<Guid>()),
                list => list.Count,
                list => list.ToList()));

        builder.HasIndex(p => new { p.PropertyId, p.Type, p.Status })
            .HasDatabaseName("ix_commercial_policies_property_type_status");

        builder.HasOne<CommercialOffer>()
            .WithMany(offer => offer.Policies)
            .HasForeignKey(p => p.PropertyId)
            .HasPrincipalKey(o => o.Id)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
