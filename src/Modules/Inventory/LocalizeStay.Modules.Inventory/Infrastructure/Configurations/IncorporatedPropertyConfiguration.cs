using LocalizeStay.Modules.Inventory.Domain.IncorporatedProperties;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace LocalizeStay.Modules.Inventory.Infrastructure.Configurations;

internal sealed class IncorporatedPropertyConfiguration : IEntityTypeConfiguration<IncorporatedProperty>
{
    public void Configure(EntityTypeBuilder<IncorporatedProperty> builder)
    {
        builder.ToTable("incorporated_properties", InventoryDbContext.SchemaName);

        builder.HasKey(p => p.Id);
        builder.Property(p => p.Id).ValueGeneratedNever();

        builder.Property(p => p.PartnerId).IsRequired();

        builder.Property(p => p.PropertyName)
            .HasColumnName("property_name")
            .HasMaxLength(180)
            .IsRequired();

        builder.Property(p => p.DestinationId)
            .HasColumnName("destination_id")
            .HasMaxLength(120)
            .IsRequired();

        builder.Property(p => p.InitialActor)
            .HasColumnName("initial_actor")
            .HasMaxLength(200)
            .IsRequired();

        builder.Property(p => p.OnboardingId)
            .HasColumnName("onboarding_id")
            .IsRequired();

        builder.Property(p => p.CreatedAt)
            .HasColumnName("created_at")
            .IsRequired();

        builder.Property(p => p.UpdatedAt)
            .HasColumnName("updated_at")
            .IsRequired();

        builder.HasIndex(p => p.OnboardingId)
            .IsUnique()
            .HasDatabaseName("ix_incorporated_properties_onboarding_id_unique");
    }
}
