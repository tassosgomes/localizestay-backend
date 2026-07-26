using System.Text.Json;
using LocalizeStay.Modules.Inventory.Domain.CommercialOffers;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace LocalizeStay.Modules.Inventory.Infrastructure.Configurations;

internal sealed class AccommodationConfiguration : IEntityTypeConfiguration<Accommodation>
{
    private static readonly JsonSerializerOptions _jsonSerializerOptions = new(JsonSerializerDefaults.Web);

    public void Configure(EntityTypeBuilder<Accommodation> builder)
    {
        builder.ToTable("accommodations", InventoryDbContext.SchemaName);

        builder.HasKey(a => a.Id);
        builder.Property(a => a.Id).ValueGeneratedNever();

        builder.Property(a => a.PropertyId)
            .HasColumnName("property_id")
            .IsRequired();

        builder.Property(a => a.CommercialName)
            .HasColumnName("commercial_name")
            .HasMaxLength(180)
            .IsRequired();

        builder.Property(a => a.Status)
            .HasColumnName("status")
            .HasConversion<string>()
            .HasMaxLength(50)
            .IsRequired();

        builder.Property(a => a.EverSubmitted)
            .HasColumnName("ever_submitted")
            .IsRequired();

        builder.Property(a => a.DeactivationReason)
            .HasColumnName("deactivation_reason")
            .HasMaxLength(1000);

        builder.Property(a => a.MaxAdults)
            .HasColumnName("max_adults");

        builder.Property(a => a.MaxChildren)
            .HasColumnName("max_children");

        builder.Property(a => a.TotalCapacity)
            .HasColumnName("total_capacity");

        builder.Property(a => a.MealPlan)
            .HasColumnName("meal_plan")
            .HasConversion<string>()
            .HasMaxLength(50);

        builder.Property(a => a.ChildAgeRangeSource)
            .HasColumnName("child_age_range_source")
            .HasConversion<string>()
            .HasMaxLength(50)
            .IsRequired();

        builder.Property(a => a.ChildMinimumAge)
            .HasColumnName("child_minimum_age");

        builder.Property(a => a.ChildMaximumAge)
            .HasColumnName("child_maximum_age");

        builder.Property(a => a.PolicyId)
            .HasColumnName("policy_id");

        builder.Property(a => a.CreatedAt)
            .HasColumnName("created_at")
            .IsRequired();

        builder.Property(a => a.UpdatedAt)
            .HasColumnName("updated_at")
            .IsRequired();

        builder.Property<List<BedEntry>>("_bedConfiguration")
            .HasColumnName("bed_configuration")
            .HasColumnType("jsonb")
            .HasConversion(
                v => JsonSerializer.Serialize(v, _jsonSerializerOptions),
                json => JsonSerializer.Deserialize<List<BedEntry>>(json, _jsonSerializerOptions) ?? new List<BedEntry>())
            .Metadata
            .SetValueComparer(new ValueComparer<List<BedEntry>>(
                (left, right) => (left ?? new List<BedEntry>()).SequenceEqual(right ?? new List<BedEntry>()),
                list => list.Count,
                list => list.ToList()));

        builder.Property<List<string>>("_structuralFeatures")
            .HasColumnName("structural_features")
            .HasColumnType("jsonb")
            .HasConversion(
                v => JsonSerializer.Serialize(v, _jsonSerializerOptions),
                json => JsonSerializer.Deserialize<List<string>>(json, _jsonSerializerOptions) ?? new List<string>())
            .Metadata
            .SetValueComparer(new ValueComparer<List<string>>(
                (left, right) => (left ?? new List<string>()).SequenceEqual(right ?? new List<string>()),
                list => list.Count,
                list => list.ToList()));

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

        builder.HasIndex(a => new { a.PropertyId, a.Status })
            .HasDatabaseName("ix_accommodations_property_status");
    }
}
