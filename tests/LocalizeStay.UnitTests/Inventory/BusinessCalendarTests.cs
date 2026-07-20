using AwesomeAssertions;
using LocalizeStay.Modules.Inventory;
using LocalizeStay.Modules.Inventory.Application.Timing;
using LocalizeStay.Modules.Inventory.Infrastructure.Timing;
using LocalizeStay.SharedKernel.Time;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace LocalizeStay.UnitTests.Inventory;

public class BusinessCalendarTests
{
    [Fact]
    public void AddBusinessDays_AcrossWeekendAndHoliday_ShouldReturnUtcDeadline()
    {
        var sut = CreateCalendar("2026-07-21");
        var start = DateTimeOffset.Parse("2026-07-17T15:30:00Z"); // Friday 12:30 in Fortaleza

        var deadline = sut.AddBusinessDays(start, 2);

        deadline.Should().Be(DateTimeOffset.Parse("2026-07-22T15:30:00Z"));
    }

    [Fact]
    public void AddBusinessDays_WithNegativeDays_ShouldThrow()
    {
        var sut = CreateCalendar();

        var act = () => sut.AddBusinessDays(DateTimeOffset.Parse("2026-07-17T15:30:00Z"), -1);

        act.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Theory]
    [InlineData("2026-07-20T11:30:00Z", true)]  // Monday 08:30 local, 3.5 business hours elapsed
    [InlineData("2026-07-20T10:58:00Z", false)] // Monday 07:58 local, more than four business hours elapsed
    [InlineData("2026-07-20T14:00:00Z", true)]  // Monday 11:00 local, same-day request
    public void IsWithinBusinessHoursSla_ForWorkingDayBoundary_ShouldEvaluateBusinessDuration(string receivedAt, bool expected)
    {
        var sut = CreateCalendar(now: "2026-07-20T15:01:00Z"); // Monday 12:01 local

        sut.IsWithinBusinessHoursSla(DateTimeOffset.Parse(receivedAt)).Should().Be(expected);
    }

    [Fact]
    public void IsWithinBusinessHoursSla_AcrossWeekendAndHoliday_ShouldExcludeNonBusinessTime()
    {
        var sut = CreateCalendar("2026-07-20", "2026-07-21T14:00:00Z"); // Tuesday 11:00 local
        var receivedAt = DateTimeOffset.Parse("2026-07-17T20:00:00Z"); // Friday 17:00 local

        sut.IsWithinBusinessHoursSla(receivedAt).Should().Be(true); // Friday 1h + Tuesday 3h
    }

    [Fact]
    public void IsWithinBusinessHoursSla_DuringWeekend_ShouldNotConsumeSla()
    {
        var sut = CreateCalendar(now: "2026-07-20T12:00:00Z"); // Monday 09:00 local
        var receivedAt = DateTimeOffset.Parse("2026-07-17T20:00:00Z"); // Friday 17:00 local

        sut.IsWithinBusinessHoursSla(receivedAt).Should().Be(true);
    }

    [Fact]
    public void AddBusinessDays_InFortalezaTimeZone_ShouldRemainStableWithoutDst()
    {
        var sut = CreateCalendar();
        var start = DateTimeOffset.Parse("2026-10-30T20:30:00Z"); // Friday 17:30 local

        sut.AddBusinessDays(start, 1).Should().Be(DateTimeOffset.Parse("2026-11-02T20:30:00Z"));
    }

    [Fact]
    public void ResolveOptions_WithMissingCalendarConfiguration_ShouldFailFast()
    {
        using var provider = CreateProvider(new Dictionary<string, string?>());

        var act = () => provider.GetRequiredService<IOptions<BusinessCalendarOptions>>().Value;

        act.Should().Throw<OptionsValidationException>()
            .WithMessage("*business calendar requires a version*");
    }

    [Fact]
    public void ResolveOptions_WithInconsistentCalendarConfiguration_ShouldFailFast()
    {
        using var provider = CreateProvider(new Dictionary<string, string?>
        {
            ["Inventory:BusinessCalendar:Version"] = "pilot-v1",
            ["Inventory:BusinessCalendar:TimeZone"] = "America/Fortaleza",
            ["Inventory:BusinessCalendar:WorkingDays:0"] = "Monday",
            ["Inventory:BusinessCalendar:StartTime"] = "18:00",
            ["Inventory:BusinessCalendar:EndTime"] = "08:00",
            ["Inventory:BusinessCalendar:CommunicationSlaBusinessHours"] = "4",
        });

        var act = () => provider.GetRequiredService<IOptions<BusinessCalendarOptions>>().Value;

        act.Should().Throw<OptionsValidationException>()
            .WithMessage("*business calendar requires a version*");
    }

    private static IBusinessCalendar CreateCalendar(string? holiday = null, string now = "2026-07-20T15:00:00Z")
    {
        var options = new BusinessCalendarOptions
        {
            Version = "test-v1",
            TimeZone = "America/Fortaleza",
            WorkingDays = ["Monday", "Tuesday", "Wednesday", "Thursday", "Friday"],
            StartTime = "08:00",
            EndTime = "18:00",
            Holidays = holiday is null ? [] : [holiday],
            CommunicationSlaBusinessHours = 4,
        };
        return new ConfiguredBusinessCalendar(Options.Create(options), new FixedClock(DateTimeOffset.Parse(now)));
    }

    private sealed class FixedClock(DateTimeOffset utcNow) : IClock
    {
        public DateTimeOffset UtcNow => utcNow;
    }

    private static ServiceProvider CreateProvider(IReadOnlyDictionary<string, string?> values)
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(values)
            .Build();
        var services = new ServiceCollection();
        new InventoryModule().RegisterServices(services, configuration);
        return services.BuildServiceProvider();
    }
}
