namespace LocalizeStay.Modules.Inventory.Application.Timing;

internal interface IBusinessCalendar
{
    public DateTimeOffset AddBusinessDays(DateTimeOffset startUtc, int businessDays);

    public bool IsWithinBusinessHoursSla(DateTimeOffset receivedAtUtc);
}
