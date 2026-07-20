using LocalizeStay.Modules.Inventory.Application.Timing;
using LocalizeStay.Modules.Inventory.Application.Upstream;
using LocalizeStay.Modules.Inventory.Endpoints;
using LocalizeStay.Modules.Inventory.Infrastructure;
using LocalizeStay.Modules.Inventory.Infrastructure.Timing;
using LocalizeStay.Modules.Inventory.Infrastructure.Upstream;
using LocalizeStay.SharedKernel.Auditing;
using LocalizeStay.SharedKernel.DependencyInjection;
using LocalizeStay.SharedKernel.Modules;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace LocalizeStay.Modules.Inventory;

/// <summary>
/// Composition-root entry point for the Inventory module. Maps a single trivial status endpoint to
/// prove the module-to-host wiring (dispatcher, DI, minimal API); no inventory business rule is
/// implemented yet (architecture baseline: guardrails against speculative behavior).
/// </summary>
public sealed class InventoryModule : IModule
{
    public string Name => "Inventory";

    public void RegisterServices(IServiceCollection services, IConfiguration configuration)
    {
        services.AddModuleDatabase<InventoryDbContext>(configuration, InventoryDbContext.SchemaName);
        services.AddModuleHandlers(typeof(InventoryModule).Assembly);
        services.AddOptions<UpstreamEligibilityOptions>()
            .Bind(configuration.GetSection(UpstreamEligibilityOptions.SectionName))
            .Validate(ValidateUpstreamEligibility, "Inventory upstream eligibility must use Pilot mode and contain non-empty, unique partner preselection and destination identifiers.")
            .ValidateOnStart();
        services.AddOptions<BusinessCalendarOptions>()
            .Bind(configuration.GetSection(BusinessCalendarOptions.SectionName))
            .Validate(ValidateBusinessCalendar, "Inventory business calendar requires a version, America/Fortaleza timezone, valid working days/hours, a non-negative unique holiday list, and a four-hour communication SLA.")
            .ValidateOnStart();
        services.AddSingleton<IPartnerPreselectionValidator, ConfiguredPartnerPreselectionValidator>();
        services.AddSingleton<IDestinationEligibilityValidator, ConfiguredDestinationEligibilityValidator>();
        services.AddSingleton<IBusinessCalendar, ConfiguredBusinessCalendar>();
        // BusinessAuditWriter<InventoryDbContext> tracks entries on this module's own DbContext
        // without committing, so mutations and their audit rows share a single SaveChangesAsync
        // (ADR-003: ownership da auditoria por módulo).
        services.AddBusinessAuditWriter<InventoryDbContext>();
    }

    public void MapEndpoints(IEndpointRouteBuilder endpoints)
    {
        endpoints.MapInventoryEndpoints();
    }

    private static bool ValidateUpstreamEligibility(UpstreamEligibilityOptions options)
        => string.Equals(options.Mode, "Pilot", StringComparison.Ordinal)
            && HasUniqueNonEmptyValues(options.EligiblePreselectionIds)
            && HasUniqueNonEmptyValues(options.ApprovedDestinationIds);

    private static bool ValidateBusinessCalendar(BusinessCalendarOptions options)
        => !string.IsNullOrWhiteSpace(options.Version)
            && string.Equals(options.TimeZone, "America/Fortaleza", StringComparison.Ordinal)
            && options.WorkingDays.Count > 0
            && options.WorkingDays.All(day => Enum.TryParse<DayOfWeek>(day, true, out _))
            && TimeOnly.TryParseExact(options.StartTime, "HH:mm", out _)
            && TimeOnly.TryParseExact(options.EndTime, "HH:mm", out _)
            && TimeOnly.ParseExact(options.StartTime, "HH:mm").CompareTo(TimeOnly.ParseExact(options.EndTime, "HH:mm")) < 0
            && options.CommunicationSlaBusinessHours == 4
            && options.Holidays.All(holiday => DateOnly.TryParseExact(holiday, "yyyy-MM-dd", out _))
            && options.Holidays.Distinct(StringComparer.Ordinal).Count() == options.Holidays.Count;

    private static bool HasUniqueNonEmptyValues(IEnumerable<string> values)
        => values.Count() > 0
            && values.All(value => !string.IsNullOrWhiteSpace(value))
            && values.Distinct(StringComparer.Ordinal).Count() == values.Count();
}
