using LocalizeStay.Modules.Inventory.Domain.CommercialOffers;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace LocalizeStay.Modules.Inventory.Infrastructure.Configurations;

internal sealed class OfferSubmissionConfiguration : IEntityTypeConfiguration<OfferSubmission>
{
    public void Configure(EntityTypeBuilder<OfferSubmission> builder)
    {
        builder.ToTable("offer_submissions", InventoryDbContext.SchemaName);

        builder.HasKey(s => s.Id);
        builder.Property(s => s.Id).ValueGeneratedNever();

        builder.Property(s => s.PropertyId)
            .HasColumnName("property_id")
            .IsRequired();

        builder.Property(s => s.Revision)
            .HasColumnName("revision")
            .IsRequired();

        builder.Property(s => s.SnapshotJson)
            .HasColumnName("snapshot_json")
            .HasColumnType("jsonb")
            .IsRequired();

        builder.Property(s => s.SubmittedBy)
            .HasColumnName("submitted_by")
            .HasMaxLength(200)
            .IsRequired();

        builder.Property(s => s.SubmittedAt)
            .HasColumnName("submitted_at")
            .IsRequired();

        builder.HasIndex(s => new { s.PropertyId, s.Revision })
            .HasDatabaseName("ix_offer_submissions_property_revision");
    }
}
