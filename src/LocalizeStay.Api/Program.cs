using LocalizeStay.Modules.Booking;
using LocalizeStay.Modules.Curation;
using LocalizeStay.Modules.CustomerCare;
using LocalizeStay.Modules.Discovery;
using LocalizeStay.Modules.IdentityAccess;
using LocalizeStay.Modules.Insights;
using LocalizeStay.Modules.Inventory;
using LocalizeStay.Modules.Operations;
using LocalizeStay.Modules.Payments;
using LocalizeStay.SharedKernel.Correlation;
using LocalizeStay.SharedKernel.ErrorHandling;
using LocalizeStay.SharedKernel.HealthChecks;
using LocalizeStay.SharedKernel.Modules;
using LocalizeStay.SharedKernel.Observability;
using LocalizeStay.SharedKernel.Security;

var builder = WebApplication.CreateBuilder(args);

// The host is the only place that knows about every module's concrete type. It orchestrates
// discovery and wiring; it never implements business behavior itself
// (architecture baseline: Architecture Style — Estrutura interna).
IReadOnlyCollection<IModule> modules =
[
    new DiscoveryModule(),
    new InventoryModule(),
    new BookingModule(),
    new PaymentsModule(),
    new CustomerCareModule(),
    new CurationModule(),
    new OperationsModule(),
    new IdentityAccessModule(),
    new InsightsModule(),
];

builder.AddLocalizeStayObservability();

builder.Services.AddOpenApi();
builder.Services.AddLocalizeStayProblemDetails();
builder.Services.AddLocalizeStayHealthChecks();
builder.Services.AddLocalizeStaySecurity(builder.Configuration);
builder.Services.AddLocalizeStayRateLimiter(builder.Configuration);
builder.Services.AddLocalizeStayModules(builder.Configuration, modules);

var app = builder.Build();

// Pipeline order is deliberate: exception handler wraps everything so no response leaks stack
// traces; correlation propagates ids end to end; auth + rate limit run before module endpoints so
// every protected route is authenticated and throttled uniformly (restful-api + production-readiness).
app.UseExceptionHandler();
app.UseCorrelationId();
app.UseLocalizeStaySecurity();
app.UseRateLimiter();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseHttpsRedirection();

app.MapLocalizeStayHealthChecks();
app.MapLocalizeStayModules(modules);

app.Run();

// Exposed so WebApplicationFactory-based integration tests can bootstrap this host later.
public partial class Program
{
}
