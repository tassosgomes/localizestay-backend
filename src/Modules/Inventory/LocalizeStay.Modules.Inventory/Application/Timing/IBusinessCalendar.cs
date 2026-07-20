namespace LocalizeStay.Modules.Inventory.Application.Timing;

internal interface IBusinessCalendar
{
    public DateTimeOffset AddBusinessDays(DateTimeOffset startUtc, int businessDays);

    public bool IsWithinBusinessHoursSla(DateTimeOffset receivedAtUtc);

    public bool IsWithinBusinessHoursSla(DateTimeOffset receivedAtUtc, DateTimeOffset processedAtUtc)
        => processedAtUtc <= receivedAtUtc || IsWithinBusinessHoursSla(receivedAtUtc);
}
