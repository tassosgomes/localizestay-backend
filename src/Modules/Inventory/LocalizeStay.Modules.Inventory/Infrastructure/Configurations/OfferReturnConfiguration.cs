using LocalizeStay.Modules.Inventory.Domain.CommercialOffers;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace LocalizeStay.Modules.Inventory.Infrastructure.Configurations;

internal sealed class OfferReturnConfiguration : IEntityTypeConfiguration<OfferReturn>
{
    public void Configure(EntityTypeBuilder<OfferReturn> builder)
    {
        builder.ToTable("offer_returns", InventoryDbContext.SchemaName);

        builder.HasKey(r => r.Id);
        builder.Property(r => r.Id).ValueGeneratedNever();

        builder.Property(r => r.PropertyId)
            .HasColumnName("property_id")
            .IsRequired();

        builder.Property(r => r.SubmissionId)
            .HasColumnName("submission_id")
            .IsRequired();

        builder.Property(r => r.Revision)
            .HasColumnName("revision")
            .IsRequired();

        builder.Property(r => r.ReasonCode)
            .HasColumnName("reason_code")
            .HasMaxLength(100)
            .IsRequired();

        builder.Property(r => r.Reason)
            .HasColumnName("reason")
            .HasMaxLength(2000)
            .IsRequired();

        builder.Property(r => r.ReturnedBy)
            .HasColumnName("returned_by")
            .HasMaxLength(200)
            .IsRequired();

        builder.Property(r => r.ReturnedAt)
            .HasColumnName("returned_at")
            .IsRequired();

        builder.HasIndex(r => new { r.PropertyId, r.SubmissionId })
            .HasDatabaseName("ix_offer_returns_property_submission");
    }
}
