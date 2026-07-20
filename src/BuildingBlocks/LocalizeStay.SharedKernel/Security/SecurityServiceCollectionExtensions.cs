using System.IdentityModel.Tokens.Jwt;
using LocalizeStay.SharedKernel.Correlation;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.Tokens;

namespace LocalizeStay.SharedKernel.Security;

/// <summary>
/// Registers JWT Bearer authentication against LogTo plus one authorization policy per
/// <see cref="PortfolioOnboardingPermissions"/> entry. Each policy combines the <c>staff</c> scope
/// (enforced at token validation) with the specific permission so endpoints just declare the policy
/// name (architecture baseline: composition root owns cross-cutting concerns; modules stay decoupled).
/// </summary>
public static class SecurityServiceCollectionExtensions
{
    public const string AuthenticationScheme = "LogToBearer";

    public static IServiceCollection AddLocalizeStaySecurity(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddOptions<LogToOptions>()
            .Bind(configuration.GetSection(LogToOptions.SectionName))
            .Validate(options => !options.ValidateConfiguration || (!string.IsNullOrWhiteSpace(options.Issuer) && !string.IsNullOrWhiteSpace(options.Audience)),
                "LogTo:Issuer and LogTo:Audience are required when LogTo:ValidateConfiguration is true. Provide them via environment variables or the secret store.")
            .ValidateOnStart();

        services.AddScoped<IAuthorizationHandler, PermissionHandler>();

        services.AddAuthentication(AuthenticationScheme)
            .AddJwtBearer(AuthenticationScheme, options =>
            {
                // LogToOptions is bound per-environment. In Development the test factory swaps these
                // values for a local issuer; in non-local environments the secret store provides them.
                var logTo = configuration.GetSection(LogToOptions.SectionName).Get<LogToOptions>() ?? new LogToOptions();

                options.Authority = logTo.Issuer;
                options.Audience = logTo.Audience;
                options.MapInboundClaims = false;
                options.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidIssuer = logTo.Issuer,
                    ValidAudience = logTo.Audience,
                    ValidateIssuer = !string.IsNullOrWhiteSpace(logTo.Issuer),
                    ValidateAudience = !string.IsNullOrWhiteSpace(logTo.Audience),
                    ValidateLifetime = true,
                    ValidateIssuerSigningKey = true,
                    RequireSignedTokens = true,
                    RequireExpirationTime = true,
                    ClockSkew = TimeSpan.FromMinutes(1),
                    NameClaimType = "sub",
                    RoleClaimType = logTo.PermissionClaimType,
                };

                // Replace the default 401 challenge response with a RFC 9457 Problem Details body
                // matching the contract (code: UNAUTHORIZED + traceId).
                options.Events = new JwtBearerEvents
                {
                    OnChallenge = async context =>
                    {
                        context.HandleResponse();
                        context.Response.StatusCode = StatusCodes.Status401Unauthorized;

                        var correlationIdAccessor = context.HttpContext.RequestServices
                            .GetRequiredService<ICorrelationIdAccessor>();

                        var problem = new ProblemDetails
                        {
                            Type = "https://api.localizestay.com/problems/unauthorized",
                            Title = "Autenticação necessária",
                            Status = StatusCodes.Status401Unauthorized,
                            Detail = "Apresente um JWT válido emitido pelo LogTo.",
                            Instance = context.HttpContext.Request.Path,
                        };
                        problem.Extensions["code"] = "UNAUTHORIZED";
                        problem.Extensions["traceId"] = correlationIdAccessor.CorrelationId;

                        await ProblemDetailsWriter.WriteAsync(context.Response, problem);
                    },
                };
            });

        services.AddAuthorizationBuilder()
            .AddPolicy(PortfolioOnboardingPermissions.Read, policy =>
            {
                policy.AuthenticationSchemes.Add(AuthenticationScheme);
                policy.RequireAuthenticatedUser();
                policy.AddRequirements(new PermissionRequirement(PortfolioOnboardingPermissions.Read));
            })
            .AddPolicy(PortfolioOnboardingPermissions.Write, policy =>
            {
                policy.AuthenticationSchemes.Add(AuthenticationScheme);
                policy.RequireAuthenticatedUser();
                policy.AddRequirements(new PermissionRequirement(PortfolioOnboardingPermissions.Write));
            })
            .AddPolicy(PortfolioOnboardingPermissions.Submit, policy =>
            {
                policy.AuthenticationSchemes.Add(AuthenticationScheme);
                policy.RequireAuthenticatedUser();
                policy.AddRequirements(new PermissionRequirement(PortfolioOnboardingPermissions.Submit));
            })
            .AddPolicy(PortfolioOnboardingPermissions.Close, policy =>
            {
                policy.AuthenticationSchemes.Add(AuthenticationScheme);
                policy.RequireAuthenticatedUser();
                policy.AddRequirements(new PermissionRequirement(PortfolioOnboardingPermissions.Close));
            })
            .AddPolicy(PortfolioOnboardingPermissions.Metrics, policy =>
            {
                policy.AuthenticationSchemes.Add(AuthenticationScheme);
                policy.RequireAuthenticatedUser();
                policy.AddRequirements(new PermissionRequirement(PortfolioOnboardingPermissions.Metrics));
            });

        return services;
    }

    /// <summary>
    /// Pins middleware ordering: authentication → correlation-aware forbidden translator →
    /// authorization → endpoint. The forbidden translator must run BEFORE authorization so its
    /// <c>await next()</c> resumes after the authorization middleware short-circuits with 403, then
    /// rewrites the empty 403 body into RFC 9457 Problem Details. Hosts must call this between
    /// <c>UseExceptionHandler</c>/<c>UseCorrelationId</c> and module endpoints.
    /// </summary>
    public static IApplicationBuilder UseLocalizeStaySecurity(this IApplicationBuilder app)
    {
        app.UseAuthentication();
        app.UseMiddleware<ForbiddenProblemDetailsMiddleware>();
        app.UseAuthorization();
        return app;
    }
}
