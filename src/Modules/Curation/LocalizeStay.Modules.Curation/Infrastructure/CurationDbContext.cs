using LocalizeStay.SharedKernel.Outbox;
using Microsoft.EntityFrameworkCore;

namespace LocalizeStay.Modules.Curation.Infrastructure;

/// <summary>
/// Owns the "curation" PostgreSQL schema. Only this module may read or write its tables (data
/// ownership rules); other modules must go through <c>LocalizeStay.Modules.Curation.Contracts</c>
/// instead (ADR-0002: um schema por módulo dono).
/// </summary>
internal sealed class CurationDbContext(DbContextOptions<CurationDbContext> options) : DbContext(options), IHasOutbox
{
    public const string SchemaName = "curation";

    public DbSet<OutboxMessage> OutboxMessages => Set<OutboxMessage>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema(SchemaName);
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(CurationDbContext).Assembly);

        modelBuilder.Entity<OutboxMessage>(outbox =>
        {
            outbox.ToTable("outbox_messages");
            outbox.HasKey(message => message.Id);
            outbox.Property(message => message.Type).HasMaxLength(500).IsRequired();
            outbox.Property(message => message.Content).IsRequired();
            outbox.Property(message => message.CorrelationId).HasMaxLength(100).IsRequired();
            outbox.HasIndex(message => new { message.ProcessedOnUtc, message.OccurredOnUtc });
        });

        base.OnModelCreating(modelBuilder);
    }
}
