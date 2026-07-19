namespace LocalizeStay.Modules.Inventory.Application.Queries;

internal sealed record InventoryModuleStatusResponse(string Module, string Status, DateTimeOffset CheckedAtUtc);
