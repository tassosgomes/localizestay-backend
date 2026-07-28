namespace LocalizeStay.Modules.Inventory.Application.Timing;

internal interface IInventoryServiceWindow
{
    public bool IsOutsideWindow(DateTimeOffset instantUtc);

    public DateTimeOffset NextWindowStart(DateTimeOffset instantUtc);

    public DateTimeOffset AddBusinessHours(DateTimeOffset startUtc, int hours);
}

internal sealed class InventoryServiceWindowOptions
{
    internal const string SectionName = "Inventory:InventoryServiceWindow";

    public string Version { get; set; } = string.Empty;

    public string TimeZone { get; set; } = "America/Fortaleza";

    public List<string> WorkingDays { get; set; } = [];

    public string StartTime { get; set; } = string.Empty;

    public string EndTime { get; set; } = string.Empty;

    public int ProcessingSlaBusinessHours { get; set; } = 4;

    public List<string> Holidays { get; set; } = [];
}
