using LocalizeStay.SharedKernel.ErrorHandling;
using LocalizeStay.SharedKernel.Security;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace LocalizeStay.IntegrationTests.Infrastructure;

/// <summary>
/// Adds a tiny test-only endpoint surface to the host so the integration suite can exercise every
/// Problem Details status code (400/404/409/422) without depending on Inventory endpoints that will
/// only exist after later tasks. The filter runs only inside the test host (it is registered by the
/// factory via <c>ConfigureTestServices</c>) and the route is protected by the same
/// <see cref="PortfolioOnboardingPermissions"/> policies used by real endpoints, so 401/403 stay
/// indistinguishable from production behavior.
/// </summary>
internal sealed class TestScenarioEndpointStartupFilter : IStartupFilter
{
    public const string Route = "/api/v1/test/scenarios/{scenario}";

    public Action<IApplicationBuilder> Configure(Action<IApplicationBuilder> next) => builder =>
    {
        next(builder);

        builder.UseEndpoints(endpoints =>
        {
            endpoints.MapGet(Route, (string scenario) => ThrowScenario(scenario))
                .WithName("TestScenario")
                .WithTags("Test")
                .RequireAuthorization(PortfolioOnboardingPermissions.Read);
        });
    };

    private static IResult ThrowScenario(string scenario) => scenario.ToUpperInvariant() switch
    {
        "OK" => Results.Ok(new { scenario = "ok" }),
        "NOTFOUND" => throw new NotFoundException(
            "A incorporação informada não existe.",
            "PROPERTY_ONBOARDING_NOT_FOUND"),
        "CONFLICT" => throw new ConflictException(
            "O identificador legal já pertence a outro parceiro.",
            "DUPLICATE_LEGAL_IDENTIFIER",
            Guid.Parse("6b22179c-0143-4a70-97d3-c9648d77666a")),
        "RULE" => throw new BusinessRuleViolationException(
            "Existem gates ou pendências que impedem o encaminhamento.",
            "ONBOARDING_NOT_READY"),
        "VALIDATION" => throw new FluentValidation.ValidationException(new[]
        {
            new FluentValidation.Results.ValidationFailure("legalIdentifier.value", "O identificador legal não atende ao formato esperado.")
            {
                ErrorCode = "INVALID_LEGAL_IDENTIFIER",
            },
        }),
        "CRASH" => throw new InvalidOperationException(
            "Database password is hunter2 and CPF is 111.222.333-44"),
        _ => throw new NotFoundException($"Scenario {scenario} is not configured."),
    };
}
