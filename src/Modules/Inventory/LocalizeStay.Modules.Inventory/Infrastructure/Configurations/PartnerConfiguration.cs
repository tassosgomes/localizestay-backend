using LocalizeStay.Modules.Inventory.Domain.Partners;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace LocalizeStay.Modules.Inventory.Infrastructure.Configurations;

internal sealed class PartnerConfiguration : IEntityTypeConfiguration<Partner>
{
    public void Configure(EntityTypeBuilder<Partner> builder)
    {
        builder.ToTable("partners", InventoryDbContext.SchemaName);

        builder.HasKey(partner => partner.Id);
        builder.Property(partner => partner.Id).ValueGeneratedNever();

        builder.Property(partner => partner.PreselectionId).HasMaxLength(100).IsRequired();
        builder.Property(partner => partner.LegalName).HasMaxLength(180).IsRequired();
        builder.Property(partner => partner.TradeName).HasMaxLength(180);
        builder.Property(partner => partner.CreatedAt).IsRequired();
        builder.Property(partner => partner.UpdatedAt).IsRequired();

        builder.OwnsOne(partner => partner.LegalIdentifier, legalIdentifier =>
        {
            legalIdentifier.Property(identifier => identifier.Type)
                .HasColumnName("legal_identifier_type")
                .HasConversion<string>()
                .HasMaxLength(30)
                .IsRequired();

            legalIdentifier.Property(identifier => identifier.CountryCode)
                .HasColumnName("legal_identifier_country_code")
                .HasMaxLength(2)
                .IsRequired();

            legalIdentifier.Property(identifier => identifier.Value)
                .HasColumnName("legal_identifier_value")
                .HasMaxLength(40)
                .IsRequired();

            legalIdentifier.Property(identifier => identifier.NormalizedValue)
                .HasColumnName("legal_identifier_normalized_value")
                .HasMaxLength(40)
                .IsRequired();

            // RF-03: one partner per normalized legal identifier (country + type + value).
            legalIdentifier.HasIndex(identifier => new { identifier.CountryCode, identifier.Type, identifier.NormalizedValue })
                .IsUnique()
                .HasDatabaseName("ix_partners_legal_identifier_unique");
        });

        builder.OwnsOne(partner => partner.PrimaryContact, contact =>
        {
            contact.Property(c => c.Name)
                .HasColumnName("contact_name")
                .HasMaxLength(120)
                .IsRequired();

            contact.Property(c => c.Email)
                .HasColumnName("contact_email")
                .HasMaxLength(255)
                .IsRequired();

            contact.Property(c => c.Phone)
                .HasColumnName("contact_phone")
                .HasMaxLength(30)
                .IsRequired();
        });

        builder.HasIndex(partner => partner.PreselectionId)
            .HasDatabaseName("ix_partners_preselection_id");

        builder.HasIndex(partner => partner.LegalName)
            .HasDatabaseName("ix_partners_legal_name");
    }
}
