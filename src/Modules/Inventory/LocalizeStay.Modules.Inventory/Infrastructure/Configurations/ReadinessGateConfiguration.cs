using LocalizeStay.Modules.Inventory.Domain.PropertyOnboardings;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace LocalizeStay.Modules.Inventory.Infrastructure.Configurations;

internal sealed class ReadinessGateConfiguration : IEntityTypeConfiguration<ReadinessGate>
{
    public void Configure(EntityTypeBuilder<ReadinessGate> builder)
    {
        builder.ToTable("readiness_gates", InventoryDbContext.SchemaName);

        builder.HasKey(gate => gate.Id);
        builder.Property(gate => gate.Id).ValueGeneratedNever();

        builder.Property(gate => gate.Type)
            .HasColumnName("type")
            .HasConversion<string>()
            .HasMaxLength(50)
            .IsRequired();

        builder.Property(gate => gate.Status)
            .HasColumnName("status")
            .HasConversion<string>()
            .HasMaxLength(50)
            .IsRequired();

        builder.Property(gate => gate.Notes).HasColumnName("notes").HasMaxLength(1000);
        builder.Property(gate => gate.ValidatedAt).HasColumnName("validated_at");
        builder.Property(gate => gate.ValidatedBy).HasColumnName("validated_by").HasMaxLength(200);
        builder.Property(gate => gate.UpdatedAt).HasColumnName("updated_at").IsRequired();

        builder.Property<Guid>("PropertyOnboardingId").IsRequired();

        builder.OwnsMany(gate => gate.Evidence, evidence =>
        {
            evidence.ToJson("evidence");
            evidence.Property(e => e.Kind).HasConversion<string>().HasMaxLength(50);
            evidence.Property(e => e.Reference).HasMaxLength(500);
            evidence.Property(e => e.Description).HasMaxLength(300);
        });

        builder.HasIndex("PropertyOnboardingId", "Type")
            .IsUnique()
            .HasDatabaseName("ix_readiness_gates_property_onboarding_id_type");

        builder.HasOne<PropertyOnboarding>()
            .WithMany(onboarding => onboarding.ReadinessGates)
            .HasForeignKey("PropertyOnboardingId")
            .HasConstraintName("fk_readiness_gates_property_onboarding_id")
            .OnDelete(DeleteBehavior.Cascade);
    }
}
