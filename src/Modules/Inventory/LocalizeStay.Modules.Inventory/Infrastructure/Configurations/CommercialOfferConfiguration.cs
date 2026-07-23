using System.Text.Json;
using LocalizeStay.Modules.Inventory.Domain.CommercialOffers;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace LocalizeStay.Modules.Inventory.Infrastructure.Configurations;

internal sealed class CommercialOfferConfiguration : IEntityTypeConfiguration<CommercialOffer>
{
    private static readonly JsonSerializerOptions _jsonSerializerOptions = new(JsonSerializerDefaults.Web);

    public void Configure(EntityTypeBuilder<CommercialOffer> builder)
    {
        builder.ToTable("commercial_offers", InventoryDbContext.SchemaName);

        builder.HasKey(o => o.Id);
        builder.Property(o => o.Id).ValueGeneratedNever();

        builder.Property(o => o.PropertyId)
            .HasColumnName("property_id")
            .IsRequired();

        builder.Property(o => o.Revision)
            .HasColumnName("revision")
            .IsConcurrencyToken()
            .IsRequired();

        builder.Property(o => o.RevisionAuthor)
            .HasColumnName("revision_author")
            .HasMaxLength(200)
            .IsRequired();

        builder.Property(o => o.State)
            .HasColumnName("state")
            .HasConversion<string>()
            .HasMaxLength(50)
            .IsRequired();

        builder.Property(o => o.AccommodationCount)
            .HasColumnName("accommodation_count")
            .IsRequired();

        builder.Property(o => o.BlockingIssueCount)
            .HasColumnName("blocking_issue_count")
            .IsRequired();

        builder.Property(o => o.CompleteInformationReceivedAt)
            .HasColumnName("complete_information_received_at");

        builder.Property(o => o.TargetSubmissionAt)
            .HasColumnName("target_submission_at");

        builder.Property(o => o.CreatedAt)
            .HasColumnName("created_at")
            .IsRequired();

        builder.Property(o => o.UpdatedAt)
            .HasColumnName("updated_at")
            .IsRequired();

        builder.Property<List<PendingIssueType>>("_pendingIssues")
            .HasColumnName("pending_issues")
            .HasColumnType("jsonb")
            .HasConversion(
                v => JsonSerializer.Serialize(v, _jsonSerializerOptions),
                json => JsonSerializer.Deserialize<List<PendingIssueType>>(json, _jsonSerializerOptions) ?? new List<PendingIssueType>())
            .Metadata
            .SetValueComparer(new ValueComparer<List<PendingIssueType>>(
                (left, right) => (left ?? new List<PendingIssueType>()).SequenceEqual(right ?? new List<PendingIssueType>()),
                list => list.Count,
                list => list.ToList()));

        builder.HasOne(o => o.CurrentValidation)
            .WithOne()
            .HasForeignKey<OfferValidation>(v => v.PropertyId);

        builder.HasMany(o => o.Submissions)
            .WithOne()
            .HasForeignKey(s => s.PropertyId);

        builder.HasMany(o => o.Returns)
            .WithOne()
            .HasForeignKey(r => r.PropertyId);

        builder.HasMany(o => o.Policies)
            .WithOne()
            .HasForeignKey(p => p.PropertyId);

        builder.HasIndex(o => o.State)
            .HasDatabaseName("ix_commercial_offers_state");
    }
}
