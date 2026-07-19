using System.Reflection;
using LocalizeStay.Modules.Booking;
using LocalizeStay.Modules.Curation;
using LocalizeStay.Modules.CustomerCare;
using LocalizeStay.Modules.Discovery;
using LocalizeStay.Modules.IdentityAccess;
using LocalizeStay.Modules.Insights;
using LocalizeStay.Modules.Inventory;
using LocalizeStay.Modules.Operations;
using LocalizeStay.Modules.Payments;
using LocalizeStay.Contracts.Booking;
using LocalizeStay.Contracts.Curation;
using LocalizeStay.Contracts.CustomerCare;
using LocalizeStay.Contracts.Discovery;
using LocalizeStay.Contracts.IdentityAccess;
using LocalizeStay.Contracts.Insights;
using LocalizeStay.Contracts.Inventory;
using LocalizeStay.Contracts.Operations;
using LocalizeStay.Contracts.Payments;

namespace LocalizeStay.ArchitectureTests;

/// <summary>
/// Every module's public <c>&lt;Name&gt;Module</c> class and Contracts marker class are the only
/// public types reachable from a module assembly, which makes them convenient anchors to get each
/// assembly under test without hard-coding file paths.
/// </summary>
internal static class ModuleAssemblies
{
    public static readonly IReadOnlyDictionary<string, Assembly> Modules = new Dictionary<string, Assembly>
    {
        ["Discovery"] = typeof(DiscoveryModule).Assembly,
        ["Inventory"] = typeof(InventoryModule).Assembly,
        ["Booking"] = typeof(BookingModule).Assembly,
        ["Payments"] = typeof(PaymentsModule).Assembly,
        ["CustomerCare"] = typeof(CustomerCareModule).Assembly,
        ["Curation"] = typeof(CurationModule).Assembly,
        ["Operations"] = typeof(OperationsModule).Assembly,
        ["IdentityAccess"] = typeof(IdentityAccessModule).Assembly,
        ["Insights"] = typeof(InsightsModule).Assembly,
    };

    public static readonly IReadOnlyDictionary<string, Assembly> Contracts = new Dictionary<string, Assembly>
    {
        ["Discovery"] = typeof(DiscoveryModuleContracts).Assembly,
        ["Inventory"] = typeof(InventoryModuleContracts).Assembly,
        ["Booking"] = typeof(BookingModuleContracts).Assembly,
        ["Payments"] = typeof(PaymentsModuleContracts).Assembly,
        ["CustomerCare"] = typeof(CustomerCareModuleContracts).Assembly,
        ["Curation"] = typeof(CurationModuleContracts).Assembly,
        ["Operations"] = typeof(OperationsModuleContracts).Assembly,
        ["IdentityAccess"] = typeof(IdentityAccessModuleContracts).Assembly,
        ["Insights"] = typeof(InsightsModuleContracts).Assembly,
    };
}
