using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace LocalizeStay.SharedKernel.Modules;

/// <summary>
/// Composition-root contract every bounded-context module implements to plug itself into the
/// LocalizeStay host. A module owns its own dependency registration and its own HTTP endpoints; the
/// host only discovers and wires modules together, it never implements business behavior itself
/// (architecture baseline: Architecture Style — Estrutura interna).
/// </summary>
public interface IModule
{
    /// <summary>
    /// Canonical module name. Matches the bounded context and, by convention, the PostgreSQL schema
    /// the module owns.
    /// </summary>
    string Name { get; }

    /// <summary>
    /// Registers the module's own services — DbContext, CQRS handlers, validators, background
    /// processors — in the composition-root container. Called once during host startup.
    /// </summary>
    void RegisterServices(IServiceCollection services, IConfiguration configuration);

    /// <summary>
    /// Maps the module's public HTTP endpoints. Called once during host startup, after routing has
    /// been configured. Modules with no public endpoints yet may leave this a no-op.
    /// </summary>
    void MapEndpoints(IEndpointRouteBuilder endpoints);
}
