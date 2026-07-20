using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using OpenTelemetry.Logs;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;

namespace LocalizeStay.SharedKernel.Observability;

/// <summary>
/// Wires OpenTelemetry traces, metrics and logs with an OTLP exporter, so every module shares the
/// same correlation/trace id end to end (architecture baseline: Observability Standards). The OTLP
/// endpoint is optional in local development — when unset, telemetry is still collected in-process
/// (useful for the ASP.NET Core instrumentation activity source) but nothing is exported.
/// </summary>
public static class OpenTelemetryExtensions
{
    public static IHostApplicationBuilder AddLocalizeStayObservability(this IHostApplicationBuilder builder)
    {
        var serviceName = builder.Configuration["OpenTelemetry:ServiceName"] ?? "localizestay-api";
        var otlpEndpoint = builder.Configuration["OpenTelemetry:OtlpEndpoint"];

        var resourceBuilder = ResourceBuilder.CreateDefault().AddService(serviceName);

        builder.Services.AddOpenTelemetry()
            .ConfigureResource(resource => resource.AddService(serviceName))
            .WithTracing(tracing => ConfigureTracing(tracing, resourceBuilder, otlpEndpoint))
            .WithMetrics(metrics => ConfigureMetrics(metrics, resourceBuilder, otlpEndpoint));

        builder.Logging.AddOpenTelemetry(logging => ConfigureLogging(logging, resourceBuilder, otlpEndpoint));

        return builder;
    }

    private static void ConfigureTracing(TracerProviderBuilder tracing, ResourceBuilder resourceBuilder, string? otlpEndpoint)
    {
        tracing.SetResourceBuilder(resourceBuilder)
            .AddAspNetCoreInstrumentation()
            .AddHttpClientInstrumentation()
            .AddSource("LocalizeStay.Inventory.Upstream")
            .AddSource("LocalizeStay.Inventory.Lifecycle");

        if (!string.IsNullOrWhiteSpace(otlpEndpoint))
        {
            tracing.AddOtlpExporter(otlp => otlp.Endpoint = new Uri(otlpEndpoint));
        }
    }

    private static void ConfigureMetrics(MeterProviderBuilder metrics, ResourceBuilder resourceBuilder, string? otlpEndpoint)
    {
        metrics.SetResourceBuilder(resourceBuilder)
            .AddAspNetCoreInstrumentation()
            .AddHttpClientInstrumentation()
            .AddMeter("LocalizeStay.Inventory.Lifecycle")
            .AddMeter("LocalizeStay.Outbox");

        if (!string.IsNullOrWhiteSpace(otlpEndpoint))
        {
            metrics.AddOtlpExporter(otlp => otlp.Endpoint = new Uri(otlpEndpoint));
        }
    }

    private static void ConfigureLogging(OpenTelemetryLoggerOptions logging, ResourceBuilder resourceBuilder, string? otlpEndpoint)
    {
        logging.SetResourceBuilder(resourceBuilder);
        logging.IncludeFormattedMessage = true;
        logging.IncludeScopes = true;

        if (!string.IsNullOrWhiteSpace(otlpEndpoint))
        {
            logging.AddOtlpExporter(otlp => otlp.Endpoint = new Uri(otlpEndpoint));
        }
    }
}
