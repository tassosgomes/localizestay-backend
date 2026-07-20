using LocalizeStay.Modules.Inventory.Domain.PropertyOnboardings;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace LocalizeStay.Modules.Inventory.Infrastructure.Configurations;

/// <summary>
/// Maps the <see cref="PropertyOnboarding"/> aggregate root and all owned/child entities into the
/// <c>inventory</c> schema. The similarity key used to prevent concurrent active onboarding cycles
/// for the same property is computed as:
/// <c>destinationId:countryCode:postalCode:normalized(street + ' ' + number)</c>.
/// </summary>
internal sealed class PropertyOnboardingConfiguration : IEntityTypeConfiguration<PropertyOnboarding>
{
    public void Configure(EntityTypeBuilder<PropertyOnboarding> builder)
    {
        builder.ToTable("property_onboardings", InventoryDbContext.SchemaName);

        builder.HasKey(onboarding => onboarding.Id);
        builder.Property(onboarding => onboarding.Id).ValueGeneratedNever();

        builder.Property(onboarding => onboarding.PartnerId).IsRequired();
        builder.Property(onboarding => onboarding.PreselectionId).HasMaxLength(100).IsRequired();

        builder.Property(onboarding => onboarding.LifecycleStatus)
            .HasColumnName("lifecycle_status")
            .HasConversion<string>()
            .HasMaxLength(50)
            .IsRequired();

        builder.Ignore(onboarding => onboarding.ReadinessStatus);

        builder.Property(onboarding => onboarding.DuplicateReviewRequiresDecision).IsRequired();
        builder.Property(onboarding => onboarding.OpenedAt).IsRequired();
        builder.Property(onboarding => onboarding.TargetSubmissionAt).IsRequired();
        builder.Property(onboarding => onboarding.SubmittedAt);
        builder.Property(onboarding => onboarding.ClosedAt);

        builder.Property(onboarding => onboarding.ReasonCode)
            .HasColumnName("close_reason_code")
            .HasConversion<string>()
            .HasMaxLength(50);

        builder.Property(onboarding => onboarding.CloseReason)
            .HasColumnName("close_reason")
            .HasMaxLength(1000);

        builder.Property(onboarding => onboarding.CreatedAt).IsRequired();
        builder.Property(onboarding => onboarding.UpdatedAt).IsRequired();

        builder.OwnsOne(onboarding => onboarding.Property, property =>
        {
            property.Property(p => p.Name)
                .HasColumnName("property_name")
                .HasMaxLength(180)
                .IsRequired();

            property.Property(p => p.DestinationId)
                .HasColumnName("property_destination_id")
                .HasMaxLength(120)
                .IsRequired();

            property.OwnsOne(p => p.Address, address =>
            {
                address.Property(a => a.Street)
                    .HasColumnName("property_address_street")
                    .HasMaxLength(180)
                    .IsRequired();

                address.Property(a => a.Number)
                    .HasColumnName("property_address_number")
                    .HasMaxLength(30)
                    .IsRequired();

                address.Property(a => a.Complement)
                    .HasColumnName("property_address_complement")
                    .HasMaxLength(120);

                address.Property(a => a.District)
                    .HasColumnName("property_address_district")
                    .HasMaxLength(120)
                    .IsRequired();

                address.Property(a => a.City)
                    .HasColumnName("property_address_city")
                    .HasMaxLength(120)
                    .IsRequired();

                address.Property(a => a.State)
                    .HasColumnName("property_address_state")
                    .HasMaxLength(80)
                    .IsRequired();

                address.Property(a => a.PostalCode)
                    .HasColumnName("property_address_postal_code")
                    .HasMaxLength(20)
                    .IsRequired();

                address.Property(a => a.CountryCode)
                    .HasColumnName("property_address_country_code")
                    .HasMaxLength(2)
                    .IsRequired();
            });
        });

        builder.Property(onboarding => onboarding.PropertySimilarityKey)
            .HasColumnName("property_similarity_key")
            .HasMaxLength(400)
            .IsRequired();

        // RF-03 / task 4.4: only one active onboarding per similarity key.
        builder.HasIndex("PropertySimilarityKey")
            .IsUnique()
            .HasDatabaseName("ix_property_onboardings_active_similarity_unique")
            .HasFilter("lifecycle_status <> 'Closed'");

        builder.HasIndex(onboarding => onboarding.LifecycleStatus)
            .HasDatabaseName("ix_property_onboardings_lifecycle_status");

        builder.HasIndex(onboarding => onboarding.PartnerId)
            .HasDatabaseName("ix_property_onboardings_partner_id");

        builder.HasIndex(onboarding => onboarding.TargetSubmissionAt)
            .HasDatabaseName("ix_property_onboardings_target_submission_at");

        builder.HasIndex(onboarding => onboarding.OpenedAt)
            .HasDatabaseName("ix_property_onboardings_opened_at");

        // ReadinessGate and CurationReturn are mapped as separate entity types (not owned) because
        // EF Core does not support JSON columns inside owned types that are mapped to their own table.
        builder.HasMany(onboarding => onboarding.ReadinessGates)
            .WithOne()
            .HasForeignKey("PropertyOnboardingId")
            .HasConstraintName("fk_readiness_gates_property_onboarding_id")
            .OnDelete(DeleteBehavior.Cascade);

        ConfigurePendingIssues(builder);
        ConfigureCommunicationRecords(builder);
        ConfigureDuplicateReviews(builder);

        builder.HasMany(onboarding => onboarding.CurationReturns)
            .WithOne()
            .HasForeignKey("PropertyOnboardingId")
            .HasConstraintName("fk_curation_returns_property_onboarding_id")
            .OnDelete(DeleteBehavior.Cascade);

        ConfigureIdempotencyKeys(builder);
    }

    private static void ConfigurePendingIssues(EntityTypeBuilder<PropertyOnboarding> builder)
    {
        builder.OwnsMany(onboarding => onboarding.PendingIssues, issue =>
        {
            issue.ToTable("pending_issues", InventoryDbContext.SchemaName);

            issue.HasKey(i => i.Id);
            issue.Property(i => i.Id).ValueGeneratedNever();

            issue.Property(i => i.Description).HasColumnName("description").HasMaxLength(1000).IsRequired();

            issue.Property(i => i.OwnerType)
                .HasColumnName("owner_type")
                .HasConversion<string>()
                .HasMaxLength(50)
                .IsRequired();

            issue.Property(i => i.AssigneeId).HasColumnName("assignee_id").HasMaxLength(120);

            issue.Property(i => i.Status)
                .HasColumnName("status")
                .HasConversion<string>()
                .HasMaxLength(50)
                .IsRequired();

            issue.Property(i => i.RelatedGateType)
                .HasColumnName("related_gate_type")
                .HasConversion<string>()
                .HasMaxLength(50);

            issue.Property(i => i.TargetAt).HasColumnName("target_at");
            issue.Property(i => i.OpenedAt).HasColumnName("opened_at").IsRequired();
            issue.Property(i => i.OpenedBy).HasColumnName("opened_by").HasMaxLength(200).IsRequired();
            issue.Property(i => i.ResolvedAt).HasColumnName("resolved_at");
            issue.Property(i => i.ResolutionNote).HasColumnName("resolution_note").HasMaxLength(1000);
            issue.Property(i => i.UpdatedAt).HasColumnName("updated_at").IsRequired();

            issue.HasIndex("PropertyOnboardingId", "Status")
                .HasDatabaseName("ix_pending_issues_property_onboarding_id_status");
        });
    }

    private static void ConfigureCommunicationRecords(EntityTypeBuilder<PropertyOnboarding> builder)
    {
        builder.OwnsMany(onboarding => onboarding.CommunicationRecords, record =>
        {
            record.ToTable("communication_records", InventoryDbContext.SchemaName);

            record.HasKey(r => r.Id);
            record.Property(r => r.Id).ValueGeneratedNever();

            record.Property(r => r.Channel)
                .HasColumnName("channel")
                .HasConversion<string>()
                .HasMaxLength(50)
                .IsRequired();

            record.Property(r => r.ReceivedAt).HasColumnName("received_at").IsRequired();
            record.Property(r => r.ProcessedAt).HasColumnName("processed_at").IsRequired();
            record.Property(r => r.ResultSummary).HasColumnName("result_summary").HasMaxLength(1000).IsRequired();
            record.Property(r => r.ProcessedWithinSla).HasColumnName("processed_within_sla").IsRequired();
            record.Property(r => r.CreatedBy).HasColumnName("created_by").HasMaxLength(200).IsRequired();
            record.Property(r => r.CreatedAt).HasColumnName("created_at").IsRequired();

            record.HasIndex("PropertyOnboardingId", "ProcessedWithinSla")
                .HasDatabaseName("ix_communication_records_property_onboarding_id_processed_within_sla");
        });
    }

    private static void ConfigureDuplicateReviews(EntityTypeBuilder<PropertyOnboarding> builder)
    {
        builder.OwnsMany(onboarding => onboarding.DuplicateReviews, review =>
        {
            review.ToTable("duplicate_reviews", InventoryDbContext.SchemaName);

            review.HasKey(r => r.Id);
            review.Property(r => r.Id).ValueGeneratedNever();

            review.Property(r => r.Decision)
                .HasColumnName("decision")
                .HasConversion<string>()
                .HasMaxLength(50)
                .IsRequired();

            review.Property(r => r.ExistingPropertyId).HasColumnName("existing_property_id");
            review.Property(r => r.Justification).HasColumnName("justification").HasMaxLength(1000).IsRequired();
            review.Property(r => r.ReviewedAt).HasColumnName("reviewed_at").IsRequired();
            review.Property(r => r.ReviewedBy).HasColumnName("reviewed_by").HasMaxLength(200).IsRequired();
            review.Property(r => r.CreatedAt).HasColumnName("created_at").IsRequired();
        });
    }

    private static void ConfigureIdempotencyKeys(EntityTypeBuilder<PropertyOnboarding> builder)
    {
        builder.HasMany(typeof(IdempotencyKey))
            .WithOne()
            .HasForeignKey("PropertyOnboardingId")
            .HasConstraintName("fk_idempotency_keys_property_onboarding_id")
            .OnDelete(DeleteBehavior.Cascade);
    }
}
