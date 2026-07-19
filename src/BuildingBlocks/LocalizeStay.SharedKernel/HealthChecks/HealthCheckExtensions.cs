using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.DependencyInjection;

namespace LocalizeStay.SharedKernel.HealthChecks;

/// <summary>
/// Exposes separate liveness and readiness probes (architecture baseline: Tracing e saúde). Liveness
/// never depends on external systems; readiness aggregates every module's own checks — e.g. its
/// database — registered by the module itself under the <see cref="ReadyTag"/> tag, so a single
/// dependency going down does not necessarily take the whole application out of service.
/// </summary>
public static class HealthCheckExtensions
{
    public const string LiveTag = "live";
    public const string ReadyTag = "ready";

    public static IServiceCollection AddLocalizeStayHealthChecks(this IServiceCollection services)
    {
        services.AddHealthChecks()
            .AddCheck("self", () => HealthCheckResult.Healthy(), tags: [LiveTag]);

        return services;
    }

    public static IEndpointRouteBuilder MapLocalizeStayHealthChecks(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapHealthChecks("/health/live", new HealthCheckOptions
        {
            Predicate = registration => registration.Tags.Contains(LiveTag),
        });

        endpoints.MapHealthChecks("/health/ready", new HealthCheckOptions
        {
            Predicate = registration => registration.Tags.Contains(ReadyTag),
        });

        return endpoints;
    }
}
