using LocalizeStay.SharedKernel.Outbox;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace LocalizeStay.SharedKernel.DependencyInjection;

/// <summary>
/// Applies the migrations owned by one module before its background services can access its schema.
/// </summary>
public sealed class ModuleDatabaseMigrationService<TDbContext>(
    IServiceScopeFactory scopeFactory,
    ILogger<ModuleDatabaseMigrationService<TDbContext>> logger) : IHostedService
    where TDbContext : DbContext, IHasOutbox
{
    public async Task StartAsync(CancellationToken cancellationToken)
    {
        await using var scope = scopeFactory.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<TDbContext>();

        logger.LogInformation("Applying database migrations for {DbContext}.", typeof(TDbContext).Name);
        await dbContext.Database.MigrateAsync(cancellationToken);
        logger.LogInformation("Database migrations applied for {DbContext}.", typeof(TDbContext).Name);
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
