using System.Diagnostics;
using Microsoft.AspNetCore.Http;

namespace LocalizeStay.SharedKernel.Correlation;

/// <summary>
/// Reads the inbound <c>X-Correlation-Id</c> header (or generates one), makes it available to the
/// whole request pipeline via <see cref="CorrelationIdAccessor"/>, tags the current trace span with
/// it, and echoes it back on the response — so every log, trace and error response for a request
/// share the same id end to end (architecture baseline: Observability Standards).
/// </summary>
public sealed class CorrelationIdMiddleware(RequestDelegate next)
{
    public const string HeaderName = "X-Correlation-Id";

    public async Task InvokeAsync(HttpContext context, CorrelationIdAccessor correlationIdAccessor)
    {
        var correlationId = ResolveCorrelationId(context);

        correlationIdAccessor.Set(correlationId);
        context.Response.Headers[HeaderName] = correlationId;
        Activity.Current?.SetTag("correlation.id", correlationId);

        await next(context);
    }

    private static string ResolveCorrelationId(HttpContext context) =>
        context.Request.Headers.TryGetValue(HeaderName, out var headerValue) && !string.IsNullOrWhiteSpace(headerValue)
            ? headerValue.ToString()
            : Guid.NewGuid().ToString("n");
}
