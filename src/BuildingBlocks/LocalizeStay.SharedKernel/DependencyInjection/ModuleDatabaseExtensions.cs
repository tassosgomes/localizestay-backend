using LocalizeStay.SharedKernel.HealthChecks;
using LocalizeStay.SharedKernel.Outbox;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace LocalizeStay.SharedKernel.DependencyInjection;

/// <summary>
/// Registers a module's PostgreSQL-backed <see cref="DbContext"/> under its own schema, plus the
/// outbox background processor and a readiness health check scoped to that schema (data ownership
/// rules: one schema per module owner, no shared database access).
/// </summary>
public static class ModuleDatabaseExtensions
{
    public static IServiceCollection AddModuleDatabase<TDbContext>(
        this IServiceCollection services,
        IConfiguration configuration,
        string schemaName)
        where TDbContext : DbContext, IHasOutbox
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(schemaName);

        services.AddDbContext<TDbContext>(options => options.UseNpgsql(
            configuration.GetConnectionString("LocalizeStay"),
            npgsql => npgsql.MigrationsHistoryTable("__ef_migrations_history", schemaName)));

        services.AddHealthChecks()
            .AddDbContextCheck<TDbContext>($"{schemaName}-database", tags: [HealthCheckExtensions.ReadyTag]);

        services.AddHostedService<OutboxProcessor<TDbContext>>();

        return services;
    }
}
