using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using LocalizeStay.SharedKernel.Security;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using Microsoft.IdentityModel.Tokens;
using Testcontainers.PostgreSql;

namespace LocalizeStay.IntegrationTests.Infrastructure;

/// <summary>
/// Bootstraps the LocalizeStay host for integration testing against a real PostgreSQL instance via
/// Testcontainers (dotnet-testing baseline). The factory replaces the LogTo bearer configuration
/// with a deterministic local issuer/signing key so tests can mint tokens carrying the staff scope
/// and the <c>portfolio-onboarding:*</c> permissions referenced by <see cref="PortfolioOnboardingPermissions"/>.
/// </summary>
public sealed class LocalizeStayWebApplicationFactory : WebApplicationFactory<Program>, IAsyncLifetime
{
    private readonly PostgreSqlContainer _dbContainer = new PostgreSqlBuilder("postgres:16-alpine")
        .WithDatabase("localizestay_tests")
        .WithUsername("localizestay_tests")
        .WithPassword("localizestay_tests")
        .Build();

    /// <summary>Deterministic signing key shared between the host and the test token issuer.</summary>
    internal static readonly SymmetricSecurityKey SigningKey = new(
        Convert.FromBase64String("QmVob2xkZXJUaGlzS2V5SXNGb3JJbnRlZ3JhdGlvblRlc3RzT25seURvTm90VXNlSW5Qcm9kdWN0aW9u"));

    internal const string TestIssuer = "https://logto.test.localizestay.com";
    internal const string TestAudience = "localizestay-api-tests";

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");

        builder.ConfigureAppConfiguration(configurationBuilder =>
        {
            configurationBuilder.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:LocalizeStay"] = _dbContainer.GetConnectionString(),
                ["LogTo:Issuer"] = TestIssuer,
                ["LogTo:Audience"] = TestAudience,
                ["LogTo:ValidateConfiguration"] = "true",
                ["RateLimit:PermitLimit"] = "100",
                ["RateLimit:TokensPerSecond"] = "100",
                ["RateLimit:ConcurrencyLimit"] = "50",
                ["RateLimit:QueueLimit"] = "0",
            });
        });

        builder.ConfigureTestServices(services =>
        {
            // The production host registers one outbox worker per module. They are deliberately
            // excluded from HTTP integration tests so asynchronous publishing cannot consume a
            // message between an endpoint response and the persistence assertions that follow.
            services.RemoveAll<IHostedService>();
            services.AddSingleton<IStartupFilter, TestScenarioEndpointStartupFilter>();

            services.PostConfigure<Microsoft.AspNetCore.Authentication.JwtBearer.JwtBearerOptions>(
                SecurityServiceCollectionExtensions.AuthenticationScheme,
                options =>
                {
                    options.Authority = TestIssuer;
                    options.Audience = TestAudience;
                    options.RequireHttpsMetadata = false;
                    options.Configuration = new Microsoft.IdentityModel.Protocols.OpenIdConnect.OpenIdConnectConfiguration
                    {
                        Issuer = TestIssuer,
                    };
                    options.TokenValidationParameters = new TokenValidationParameters
                    {
                        ValidIssuer = TestIssuer,
                        ValidAudience = TestAudience,
                        ValidateIssuer = true,
                        ValidateAudience = true,
                        ValidateLifetime = true,
                        ValidateIssuerSigningKey = true,
                        RequireSignedTokens = true,
                        RequireExpirationTime = true,
                        ClockSkew = TimeSpan.FromMinutes(1),
                        IssuerSigningKey = SigningKey,
                        NameClaimType = "sub",
                    };
                });
        });
    }

    public async Task InitializeAsync()
    {
        await _dbContainer.StartAsync();
    }

    public new async Task DisposeAsync()
    {
        await _dbContainer.DisposeAsync();
        await base.DisposeAsync();
    }

    /// <summary>
    /// Mints a signed JWT carrying <c>scope: staff</c> and the provided permissions, suitable for
    /// exercising endpoints protected by <see cref="PortfolioOnboardingPermissions"/>. Static so
    /// callers can use it from any factory instance, including those created via
    /// <c>WebApplicationFactory.WithWebHostBuilder</c>.
    /// </summary>
    public static string CreateToken(string subject, params string[] permissions)
    {
        var claims = new List<Claim>
        {
            new("sub", subject),
            new("scope", "staff"),
        };
        foreach (var permission in permissions)
        {
            claims.Add(new Claim("permission", permission));
        }

        var now = DateTimeOffset.UtcNow;
        var descriptor = new SecurityTokenDescriptor
        {
            Issuer = TestIssuer,
            Audience = TestAudience,
            Subject = new ClaimsIdentity(claims),
            NotBefore = now.UtcDateTime,
            Expires = now.Add(TimeSpan.FromHours(1)).UtcDateTime,
            SigningCredentials = new SigningCredentials(SigningKey, SecurityAlgorithms.HmacSha256),
        };

        var handler = new JwtSecurityTokenHandler();
        return handler.WriteToken(handler.CreateToken(descriptor));
    }
}
