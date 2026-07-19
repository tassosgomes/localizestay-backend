using LocalizeStay.Modules.Inventory.Domain.PropertyOnboardings;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace LocalizeStay.Modules.Inventory.Infrastructure.Configurations;

internal sealed class CurationReturnConfiguration : IEntityTypeConfiguration<CurationReturn>
{
    public void Configure(EntityTypeBuilder<CurationReturn> builder)
    {
        builder.ToTable("curation_returns", InventoryDbContext.SchemaName);

        builder.HasKey(curationReturn => curationReturn.Id);
        builder.Property(curationReturn => curationReturn.Id).ValueGeneratedNever();

        builder.Property(curationReturn => curationReturn.CurationReference)
            .HasColumnName("curation_reference")
            .HasMaxLength(120);

        builder.Property(curationReturn => curationReturn.ReasonCode)
            .HasColumnName("reason_code")
            .HasConversion<string>()
            .HasMaxLength(50)
            .IsRequired();

        builder.Property(curationReturn => curationReturn.Reason)
            .HasColumnName("reason")
            .HasMaxLength(1000)
            .IsRequired();

        builder.Property(curationReturn => curationReturn.ReturnedAt)
            .HasColumnName("returned_at")
            .IsRequired();

        builder.Property(curationReturn => curationReturn.ReturnedBy)
            .HasColumnName("returned_by")
            .HasMaxLength(200)
            .IsRequired();

        builder.Property(curationReturn => curationReturn.CreatedAt)
            .HasColumnName("created_at")
            .IsRequired();

        builder.Property<Guid>("PropertyOnboardingId").IsRequired();

        builder.OwnsMany(curationReturn => curationReturn.Issues, issue =>
        {
            issue.ToJson("issues");
            issue.Property(i => i.Description).HasMaxLength(1000);
            issue.Property(i => i.OwnerType).HasConversion<string>().HasMaxLength(50);
            issue.Property(i => i.RelatedGateType).HasConversion<string>().HasMaxLength(50);
        });

        builder.HasIndex("PropertyOnboardingId", "ReturnedAt")
            .HasDatabaseName("ix_curation_returns_property_onboarding_id_returned_at");

        builder.HasOne<PropertyOnboarding>()
            .WithMany(onboarding => onboarding.CurationReturns)
            .HasForeignKey("PropertyOnboardingId")
            .HasConstraintName("fk_curation_returns_property_onboarding_id")
            .OnDelete(DeleteBehavior.Cascade);
    }
}
