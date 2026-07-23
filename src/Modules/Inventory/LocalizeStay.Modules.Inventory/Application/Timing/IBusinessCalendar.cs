namespace LocalizeStay.Modules.Inventory.Application.Timing;

internal interface IBusinessCalendar
{
    public DateTimeOffset AddBusinessDays(DateTimeOffset startUtc, int businessDays);

    public bool IsWithinBusinessDays(DateTimeOffset startUtc, DateTimeOffset endUtc, int businessDays)
        => endUtc <= AddBusinessDays(startUtc, businessDays);

    public bool IsWithinBusinessHoursSla(DateTimeOffset receivedAtUtc);

    public bool IsWithinBusinessHoursSla(DateTimeOffset receivedAtUtc, DateTimeOffset processedAtUtc)
        => processedAtUtc <= receivedAtUtc || IsWithinBusinessHoursSla(receivedAtUtc);
}
