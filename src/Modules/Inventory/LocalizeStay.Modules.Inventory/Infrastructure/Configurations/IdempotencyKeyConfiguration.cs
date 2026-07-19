using LocalizeStay.Modules.Inventory.Domain.PropertyOnboardings;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace LocalizeStay.Modules.Inventory.Infrastructure.Configurations;

internal sealed class IdempotencyKeyConfiguration : IEntityTypeConfiguration<IdempotencyKey>
{
    public void Configure(EntityTypeBuilder<IdempotencyKey> builder)
    {
        builder.ToTable("idempotency_keys", InventoryDbContext.SchemaName);

        builder.HasKey(key => key.Id);
        builder.Property(key => key.Id).ValueGeneratedNever();

        builder.Property(key => key.PropertyOnboardingId).IsRequired();
        builder.Property(key => key.Key).HasColumnName("key").IsRequired();

        builder.Property(key => key.Scope)
            .HasColumnName("scope")
            .HasConversion<string>()
            .HasMaxLength(50)
            .IsRequired();

        builder.Property(key => key.CreatedAt)
            .HasColumnName("created_at")
            .IsRequired();

        builder.HasIndex(key => new { key.PropertyOnboardingId, key.Key, key.Scope })
            .IsUnique()
            .HasDatabaseName("ix_idempotency_keys_property_onboarding_id_key_scope");

        builder.HasOne<PropertyOnboarding>()
            .WithMany()
            .HasForeignKey(key => key.PropertyOnboardingId)
            .HasConstraintName("fk_idempotency_keys_property_onboarding_id")
            .OnDelete(DeleteBehavior.Cascade);
    }
}
