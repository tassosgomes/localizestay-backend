using Microsoft.EntityFrameworkCore;

namespace LocalizeStay.SharedKernel.Outbox;

/// <summary>Implemented by every module's <see cref="DbContext"/> to expose its own outbox table.</summary>
public interface IHasOutbox
{
    DbSet<OutboxMessage> OutboxMessages { get; }
}
