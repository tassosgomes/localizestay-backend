using Microsoft.AspNetCore.Builder;

namespace LocalizeStay.SharedKernel.Correlation;

public static class CorrelationIdApplicationBuilderExtensions
{
    public static IApplicationBuilder UseCorrelationId(this IApplicationBuilder app) =>
        app.UseMiddleware<CorrelationIdMiddleware>();
}
