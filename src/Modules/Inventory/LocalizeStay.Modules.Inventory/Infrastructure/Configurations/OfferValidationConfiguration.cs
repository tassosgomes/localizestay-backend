using LocalizeStay.Modules.Inventory.Domain.CommercialOffers;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace LocalizeStay.Modules.Inventory.Infrastructure.Configurations;

internal sealed class OfferValidationConfiguration : IEntityTypeConfiguration<OfferValidation>
{
    public void Configure(EntityTypeBuilder<OfferValidation> builder)
    {
        builder.ToTable("offer_validations", InventoryDbContext.SchemaName);

        builder.HasKey(v => v.Id);
        builder.Property(v => v.Id).ValueGeneratedNever();

        builder.Property(v => v.PropertyId)
            .HasColumnName("property_id")
            .IsRequired();

        builder.Property(v => v.Revision)
            .HasColumnName("revision")
            .IsRequired();

        builder.Property(v => v.ValidatedBy)
            .HasColumnName("validated_by")
            .HasMaxLength(200)
            .IsRequired();

        builder.Property(v => v.ValidatedAt)
            .HasColumnName("validated_at")
            .IsRequired();

        builder.Property(v => v.Status)
            .HasColumnName("status")
            .HasConversion<string>()
            .HasMaxLength(50)
            .IsRequired();

        builder.Property(v => v.InvalidatedAt)
            .HasColumnName("invalidated_at");

        builder.Property(v => v.InvalidationReason)
            .HasColumnName("invalidation_reason")
            .HasMaxLength(500);

        builder.Property(v => v.Comment)
            .HasColumnName("comment")
            .HasMaxLength(1_000);

        builder.HasIndex(v => new { v.PropertyId, v.Revision })
            .HasDatabaseName("ix_offer_validations_property_revision");
    }
}
