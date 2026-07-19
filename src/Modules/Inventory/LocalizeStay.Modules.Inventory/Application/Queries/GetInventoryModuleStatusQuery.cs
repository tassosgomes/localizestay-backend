using LocalizeStay.SharedKernel.Cqrs;

namespace LocalizeStay.Modules.Inventory.Application.Queries;

/// <summary>
/// Trivial status query used only to prove the module-to-host wiring end to end (dispatcher,
/// handler resolution and minimal API mapping). It intentionally carries no business rule.
/// </summary>
internal sealed record GetInventoryModuleStatusQuery : IQuery<InventoryModuleStatusResponse>;
