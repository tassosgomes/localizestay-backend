using System.Text.Json;
using LocalizeStay.Modules.Inventory.Domain.CommercialOffers;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace LocalizeStay.Modules.Inventory.Infrastructure.Configurations;

internal sealed class CommercialRateConfiguration : IEntityTypeConfiguration<CommercialRate>
{
    private static readonly JsonSerializerOptions _jsonSerializerOptions = new(JsonSerializerDefaults.Web);

    public void Configure(EntityTypeBuilder<CommercialRate> builder)
    {
        builder.ToTable("commercial_rates", InventoryDbContext.SchemaName);

        builder.HasKey(r => r.Id);
        builder.Property(r => r.Id).ValueGeneratedNever();

        builder.Property(r => r.PropertyId)
            .HasColumnName("property_id")
            .IsRequired();

        builder.Property(r => r.AccommodationId)
            .HasColumnName("accommodation_id")
            .IsRequired();

        builder.Property(r => r.Name)
            .HasColumnName("name")
            .HasMaxLength(120)
            .IsRequired();

        builder.Property(r => r.ConditionCode)
            .HasColumnName("condition_code")
            .HasMaxLength(60)
            .IsRequired();

        builder.Property(r => r.BasePriceCents)
            .HasColumnName("base_price_cents");

        builder.Property(r => r.IncludedGuests)
            .HasColumnName("included_guests");

        builder.Property(r => r.AdditionalAdultPriceCents)
            .HasColumnName("additional_adult_price_cents");

        builder.Property(r => r.AdditionalChildPriceCents)
            .HasColumnName("additional_child_price_cents");

        builder.Property(r => r.ValidFrom)
            .HasColumnName("valid_from");

        builder.Property(r => r.ValidTo)
            .HasColumnName("valid_to");

        builder.Property(r => r.MinimumNights)
            .HasColumnName("minimum_nights");

        builder.Property(r => r.PolicyId)
            .HasColumnName("policy_id");

        builder.Property(r => r.MealPlan)
            .HasColumnName("meal_plan")
            .HasConversion<string>()
            .HasMaxLength(50);

        builder.Property(r => r.Status)
            .HasColumnName("status")
            .HasConversion<string>()
            .HasMaxLength(50)
            .IsRequired();

        builder.Property(r => r.DeactivationReason)
            .HasColumnName("deactivation_reason")
            .HasMaxLength(500);

        builder.Property(r => r.EverSubmitted)
            .HasColumnName("ever_submitted")
            .IsRequired();

        builder.Property(r => r.CreatedAt)
            .HasColumnName("created_at")
            .IsRequired();

        builder.Property(r => r.UpdatedAt)
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

        builder.HasIndex(r => new { r.AccommodationId, r.ConditionCode, r.PolicyId, r.MealPlan, r.ValidFrom, r.ValidTo })
            .HasDatabaseName("ix_commercial_rates_overlap");

        builder.HasIndex(r => new { r.PropertyId, r.Status })
            .HasDatabaseName("ix_commercial_rates_property_status");

        builder.HasIndex(r => r.AccommodationId)
            .HasDatabaseName("ix_commercial_rates_accommodation");
    }
}
