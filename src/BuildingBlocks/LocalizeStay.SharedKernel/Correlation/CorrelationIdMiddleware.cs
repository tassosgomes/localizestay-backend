using System.Diagnostics;
using LocalizeStay.SharedKernel.ErrorHandling;
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

    /// <summary>
    /// Key under which the correlation id is mirrored into <see cref="HttpContext.Items"/>. The
    /// <see cref="AsyncLocal{T}"/> backing <see cref="CorrelationIdAccessor"/> does not survive
    /// exception propagation upstream, so we keep a parallel copy that the
    /// <see cref="GlobalExceptionHandler"/> can read with the original <c>HttpContext</c> in hand.
    /// </summary>
    public const string ItemsKey = "__LocalizeStay.CorrelationId";

    public async Task InvokeAsync(HttpContext context, CorrelationIdAccessor correlationIdAccessor)
    {
        var correlationId = ResolveCorrelationId(context);

        correlationIdAccessor.Set(correlationId);
        context.Items[ItemsKey] = correlationId;
        context.Response.Headers[HeaderName] = correlationId;
        context.TraceIdentifier = correlationId;
        Activity.Current?.SetTag("correlation.id", correlationId);

        await next(context);
    }

    private static string ResolveCorrelationId(HttpContext context) =>
        context.Request.Headers.TryGetValue(HeaderName, out var headerValue) && !string.IsNullOrWhiteSpace(headerValue)
            ? headerValue.ToString()
            : Guid.NewGuid().ToString("n");
}
