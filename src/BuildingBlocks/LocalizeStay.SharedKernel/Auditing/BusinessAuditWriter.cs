using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace LocalizeStay.SharedKernel.Auditing;

/// <summary>
/// Adds <see cref="BusinessAuditEntry"/> rows to the owning module's <typeparamref name="TDbContext"/>
/// without calling <c>SaveChanges</c>. One closed generic type per module keeps data ownership
/// scoped to each module's own schema (ADR-003): only the module that owns the <c>DbContext</c>
/// resolves its own writer registration, so the Shared Kernel never reaches across schemas.
/// </summary>
public sealed class BusinessAuditWriter<TDbContext>(TDbContext dbContext) : IBusinessAuditWriter
    where TDbContext : DbContext
{
    private readonly TDbContext _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));

    /// <inheritdoc />
    public void Record(BusinessAuditEntry entry)
    {
        ArgumentNullException.ThrowIfNull(entry);
        _dbContext.Set<BusinessAuditEntry>().Add(entry);
    }
}

/// <summary>
/// DI helper for modules to register their own <see cref="IBusinessAuditWriter"/> closed generic
/// type against their own <c>DbContext</c>, keeping the registration close to the schema owner.
/// </summary>
public static class BusinessAuditWriterExtensions
{
    /// <summary>Registers <see cref="IBusinessAuditWriter"/> as a scoped <c>BusinessAuditWriter&lt;TDbContext&gt;</c>.</summary>
    public static IServiceCollection AddBusinessAuditWriter<TDbContext>(this IServiceCollection services)
        where TDbContext : DbContext
    {
        ArgumentNullException.ThrowIfNull(services);
        services.AddScoped<IBusinessAuditWriter, BusinessAuditWriter<TDbContext>>();
        return services;
    }
}
