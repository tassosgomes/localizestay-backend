using System.Net.Mime;
using System.Threading.RateLimiting;
using LocalizeStay.SharedKernel.Correlation;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace LocalizeStay.SharedKernel.Security;

/// <summary>
/// Non-sensitive rate-limit tuning. Secrets stay out of <c>appsettings.json</c>
/// (production-readiness baseline); this only exposes the values that operators may tune without a
/// redeploy. The actual limiter is registered by <see cref="RateLimitingServiceCollectionExtensions"/>.
/// </summary>
public sealed class RateLimitOptions
{
    public const string SectionName = "RateLimit";

    /// <summary>Maximum concurrent in-flight requests per authenticated token bucket.</summary>
    public int ConcurrencyLimit { get; set; } = 50;

    /// <summary>Token bucket capacity per identity.</summary>
    public int PermitLimit { get; set; } = 100;

    /// <summary>Tokens restored per second per identity.</summary>
    public int TokensPerSecond { get; set; } = 20;

    /// <summary>Maximum queue depth before rejecting with 429.</summary>
    public int QueueLimit { get; set; } = 10;
}

/// <summary>
/// Registers the ASP.NET Core rate limiter using a per-token bucket keyed by the authenticated
/// <c>sub</c>. When the limiter rejects a request, the response is rewritten as RFC 9457 Problem
/// Details with <c>code: RATE_LIMIT_EXCEEDED</c> and a <c>Retry-After</c> hint, matching the
/// OpenAPI contract field by field.
/// </summary>
public static class RateLimitingServiceCollectionExtensions
{
    public static IServiceCollection AddLocalizeStayRateLimiter(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddOptions<RateLimitOptions>()
            .Bind(configuration.GetSection(RateLimitOptions.SectionName));

        services.AddRateLimiter(options =>
        {
            options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;

            options.OnRejected = async (context, _) =>
            {
                var httpContext = context.HttpContext;
                if (httpContext.Response.HasStarted)
                {
                    return;
                }

                httpContext.Response.StatusCode = StatusCodes.Status429TooManyRequests;
                httpContext.Response.Headers.RetryAfter = "30";

                var correlationIdAccessor = httpContext.RequestServices
                    .GetRequiredService<ICorrelationIdAccessor>();

                var problem = new ProblemDetails
                {
                    Type = "https://api.localizestay.com/problems/rate-limit",
                    Title = "Muitas requisições",
                    Status = StatusCodes.Status429TooManyRequests,
                    Detail = "Aguarde antes de tentar novamente.",
                    Instance = httpContext.Request.Path,
                };
                problem.Extensions["code"] = "RATE_LIMIT_EXCEEDED";
                problem.Extensions["traceId"] = correlationIdAccessor.CorrelationId;

                await ProblemDetailsWriter.WriteAsync(httpContext.Response, problem);
            };

            var rateLimitOptions = configuration.GetSection(RateLimitOptions.SectionName).Get<RateLimitOptions>() ?? new RateLimitOptions();

            options.GlobalLimiter = PartitionedRateLimiter.Create<HttpContext, string>(httpContext =>
            {
                var principal = httpContext.User;
                var partitionKey = principal.Identity?.IsAuthenticated == true
                    ? (principal.FindFirst("sub")?.Value ?? "anonymous")
                    : "anonymous";

                return RateLimitPartition.GetTokenBucketLimiter(
                    partitionKey,
                    _ => new TokenBucketRateLimiterOptions
                    {
                        AutoReplenishment = true,
                        TokensPerPeriod = rateLimitOptions.TokensPerSecond,
                        ReplenishmentPeriod = TimeSpan.FromSeconds(1),
                        TokenLimit = rateLimitOptions.PermitLimit,
                        QueueLimit = rateLimitOptions.QueueLimit,
                        QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
                    });
            });

            options.AddPolicy("concurrency", httpContext =>
            {
                var partitionKey = httpContext.User.Identity?.IsAuthenticated == true
                    ? (httpContext.User.FindFirst("sub")?.Value ?? "anonymous")
                    : "anonymous";

                return RateLimitPartition.GetConcurrencyLimiter(
                    partitionKey,
                    _ => new ConcurrencyLimiterOptions
                    {
                        PermitLimit = rateLimitOptions.ConcurrencyLimit,
                        QueueLimit = rateLimitOptions.QueueLimit,
                        QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
                    });
            });
        });

        return services;
    }
}
