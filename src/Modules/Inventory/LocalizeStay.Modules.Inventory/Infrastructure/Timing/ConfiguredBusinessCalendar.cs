using System.Globalization;
using LocalizeStay.Modules.Inventory.Application.Timing;
using LocalizeStay.SharedKernel.Time;
using Microsoft.Extensions.Options;

namespace LocalizeStay.Modules.Inventory.Infrastructure.Timing;

internal sealed class BusinessCalendarOptions
{
    internal const string SectionName = "Inventory:BusinessCalendar";

    public string Version { get; set; } = string.Empty;

    public string TimeZone { get; set; } = "America/Fortaleza";

    public List<string> WorkingDays { get; set; } = [];

    public string StartTime { get; set; } = string.Empty;

    public string EndTime { get; set; } = string.Empty;

    public List<string> Holidays { get; set; } = [];

    public int CommunicationSlaBusinessHours { get; set; } = 4;
}

internal sealed class ConfiguredBusinessCalendar : IBusinessCalendar
{
    private readonly IClock _clock;
    private readonly TimeZoneInfo _timeZone;
    private readonly HashSet<DayOfWeek> _workingDays;
    private readonly HashSet<DateOnly> _holidays;
    private readonly TimeOnly _startTime;
    private readonly TimeOnly _endTime;
    private readonly TimeSpan _communicationSla;

    public ConfiguredBusinessCalendar(IOptions<BusinessCalendarOptions> options, IClock clock)
    {
        _clock = clock;
        var value = options.Value;
        _timeZone = TimeZoneInfo.FindSystemTimeZoneById(value.TimeZone);
        _workingDays = value.WorkingDays.Select(ParseDayOfWeek).ToHashSet();
        _holidays = value.Holidays.Select(ParseDate).ToHashSet();
        _startTime = TimeOnly.ParseExact(value.StartTime, "HH:mm", CultureInfo.InvariantCulture);
        _endTime = TimeOnly.ParseExact(value.EndTime, "HH:mm", CultureInfo.InvariantCulture);
        _communicationSla = TimeSpan.FromHours(value.CommunicationSlaBusinessHours);
    }

    public DateTimeOffset AddBusinessDays(DateTimeOffset startUtc, int businessDays)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(businessDays);

        var localStart = TimeZoneInfo.ConvertTime(startUtc, _timeZone);
        var date = DateOnly.FromDateTime(localStart.Date);

        for (var addedDays = 0; addedDays < businessDays;)
        {
            date = date.AddDays(1);
            if (IsBusinessDay(date))
            {
                addedDays++;
            }
        }

        var localDeadline = new DateTimeOffset(
            date.ToDateTime(TimeOnly.FromDateTime(localStart.DateTime), DateTimeKind.Unspecified),
            _timeZone.GetUtcOffset(date.ToDateTime(TimeOnly.MinValue)));
        return localDeadline.ToUniversalTime();
    }

    public bool IsWithinBusinessHoursSla(DateTimeOffset receivedAtUtc)
    {
        if (receivedAtUtc > _clock.UtcNow)
        {
            return true;
        }

        return CalculateBusinessDuration(receivedAtUtc, _clock.UtcNow) <= _communicationSla;
    }

    private TimeSpan CalculateBusinessDuration(DateTimeOffset startUtc, DateTimeOffset endUtc)
    {
        var localStart = TimeZoneInfo.ConvertTime(startUtc, _timeZone);
        var localEnd = TimeZoneInfo.ConvertTime(endUtc, _timeZone);
        var date = DateOnly.FromDateTime(localStart.Date);
        var finalDate = DateOnly.FromDateTime(localEnd.Date);
        var total = TimeSpan.Zero;

        while (date <= finalDate)
        {
            if (IsBusinessDay(date))
            {
                var businessStart = ToLocalInstant(date, _startTime);
                var businessEnd = ToLocalInstant(date, _endTime);
                var intervalStart = Max(businessStart, startUtc);
                var intervalEnd = Min(businessEnd, endUtc);
                if (intervalEnd > intervalStart)
                {
                    total += intervalEnd - intervalStart;
                }
            }

            date = date.AddDays(1);
        }

        return total;
    }

    private bool IsBusinessDay(DateOnly date) => _workingDays.Contains(date.DayOfWeek) && !_holidays.Contains(date);

    private DateTimeOffset ToLocalInstant(DateOnly date, TimeOnly time)
    {
        var localDateTime = date.ToDateTime(time, DateTimeKind.Unspecified);
        return new DateTimeOffset(localDateTime, _timeZone.GetUtcOffset(localDateTime)).ToUniversalTime();
    }

    private static DateOnly ParseDate(string value) => DateOnly.ParseExact(value, "yyyy-MM-dd", CultureInfo.InvariantCulture);

    private static DayOfWeek ParseDayOfWeek(string value) => Enum.Parse<DayOfWeek>(value, ignoreCase: true);

    private static DateTimeOffset Max(DateTimeOffset left, DateTimeOffset right) => left >= right ? left : right;

    private static DateTimeOffset Min(DateTimeOffset left, DateTimeOffset right) => left <= right ? left : right;
}
