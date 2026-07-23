using LocalizeStay.Modules.Inventory.Domain.CommercialOffers;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace LocalizeStay.Modules.Inventory.Infrastructure.Configurations;

internal sealed class CommercialOfferIdempotencyKeyConfiguration : IEntityTypeConfiguration<CommercialOfferIdempotencyKey>
{
    public void Configure(EntityTypeBuilder<CommercialOfferIdempotencyKey> builder)
    {
        builder.ToTable("commercial_offer_idempotency_keys", InventoryDbContext.SchemaName);

        builder.HasKey(k => k.Id);
        builder.Property(k => k.Id).ValueGeneratedNever();

        builder.Property(k => k.PropertyId)
            .HasColumnName("property_id")
            .IsRequired();

        builder.Property(k => k.Key)
            .HasColumnName("key")
            .IsRequired();

        builder.Property(k => k.Scope)
            .HasColumnName("scope")
            .IsRequired()
            .HasMaxLength(50);

        builder.Property(k => k.PayloadFingerprint)
            .HasColumnName("payload_fingerprint")
            .HasMaxLength(64);

        builder.Property(k => k.ResultReferenceId)
            .HasColumnName("result_reference_id");

        builder.Property(k => k.CreatedAt)
            .HasColumnName("created_at")
            .IsRequired();

        builder.HasIndex(k => new { k.PropertyId, k.Key, k.Scope })
            .IsUnique()
            .HasDatabaseName("ix_commercial_offer_idempotency_keys_property_key_scope");

        builder.HasOne<CommercialOffer>()
            .WithMany()
            .HasForeignKey(k => k.PropertyId)
            .HasConstraintName("fk_commercial_offer_idempotency_keys_offer_id")
            .OnDelete(DeleteBehavior.Cascade);
    }
}
