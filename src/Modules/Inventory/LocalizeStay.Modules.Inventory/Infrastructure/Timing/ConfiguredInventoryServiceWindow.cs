using System.Globalization;
using LocalizeStay.Modules.Inventory.Application.Timing;
using Microsoft.Extensions.Options;

namespace LocalizeStay.Modules.Inventory.Infrastructure.Timing;

internal sealed class ConfiguredInventoryServiceWindow : IInventoryServiceWindow
{
    private readonly TimeZoneInfo _timeZone;
    private readonly HashSet<DayOfWeek> _workingDays;
    private readonly HashSet<DateOnly> _holidays;
    private readonly TimeOnly _startTime;
    private readonly TimeOnly _endTime;

    public ConfiguredInventoryServiceWindow(IOptions<InventoryServiceWindowOptions> options)
    {
        var value = options.Value;
        _timeZone = TimeZoneInfo.FindSystemTimeZoneById(value.TimeZone);
        _workingDays = value.WorkingDays.Select(ParseDayOfWeek).ToHashSet();
        _holidays = value.Holidays.Select(ParseDate).ToHashSet();
        _startTime = TimeOnly.ParseExact(value.StartTime, "HH:mm", CultureInfo.InvariantCulture);
        _endTime = TimeOnly.ParseExact(value.EndTime, "HH:mm", CultureInfo.InvariantCulture);
    }

    public bool IsOutsideWindow(DateTimeOffset instantUtc)
    {
        var localInstant = TimeZoneInfo.ConvertTime(instantUtc, _timeZone);
        var date = DateOnly.FromDateTime(localInstant.Date);
        var time = TimeOnly.FromDateTime(localInstant.DateTime);
        return !IsBusinessDay(date) || time < _startTime || time >= _endTime;
    }

    public DateTimeOffset NextWindowStart(DateTimeOffset instantUtc)
    {
        if (!IsOutsideWindow(instantUtc))
        {
            return instantUtc;
        }

        var localInstant = TimeZoneInfo.ConvertTime(instantUtc, _timeZone);
        var date = DateOnly.FromDateTime(localInstant.Date);
        if (IsBusinessDay(date) && TimeOnly.FromDateTime(localInstant.DateTime) < _startTime)
        {
            return ToUtcInstant(date, _startTime);
        }

        do
        {
            date = date.AddDays(1);
        }
        while (!IsBusinessDay(date));

        return ToUtcInstant(date, _startTime);
    }

    public DateTimeOffset AddBusinessHours(DateTimeOffset startUtc, int hours)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(hours);

        var current = NextWindowStart(startUtc);
        var remaining = TimeSpan.FromHours(hours);
        while (remaining > TimeSpan.Zero)
        {
            var localCurrent = TimeZoneInfo.ConvertTime(current, _timeZone);
            var end = ToUtcInstant(DateOnly.FromDateTime(localCurrent.Date), _endTime);
            var available = end - current;
            if (remaining <= available)
            {
                return current.Add(remaining);
            }

            remaining -= available;
            current = NextWindowStart(end);
        }

        return current;
    }

    private bool IsBusinessDay(DateOnly date) => _workingDays.Contains(date.DayOfWeek) && !_holidays.Contains(date);

    private DateTimeOffset ToUtcInstant(DateOnly date, TimeOnly time)
    {
        var localDateTime = date.ToDateTime(time, DateTimeKind.Unspecified);
        return new DateTimeOffset(localDateTime, _timeZone.GetUtcOffset(localDateTime)).ToUniversalTime();
    }

    private static DateOnly ParseDate(string value) => DateOnly.ParseExact(value, "yyyy-MM-dd", CultureInfo.InvariantCulture);

    private static DayOfWeek ParseDayOfWeek(string value) => Enum.Parse<DayOfWeek>(value, ignoreCase: true);
}
