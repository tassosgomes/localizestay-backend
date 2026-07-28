using AwesomeAssertions;
using LocalizeStay.Modules.Inventory;
using LocalizeStay.Modules.Inventory.Application.Timing;
using LocalizeStay.Modules.Inventory.Infrastructure.Timing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace LocalizeStay.UnitTests.Inventory;

public class InventoryServiceWindowTests
{
    [Theory]
    [InlineData("2026-07-20T11:00:00Z", false)] // Monday 08:00 local
    [InlineData("2026-07-20T10:59:00Z", true)]  // Monday 07:59 local
    [InlineData("2026-07-25T22:59:00Z", false)] // Saturday 19:59 local
    [InlineData("2026-07-25T23:00:00Z", true)]  // Saturday 20:00 local
    [InlineData("2026-07-26T15:00:00Z", true)]  // Sunday 12:00 local
    public void IsOutsideWindow_ForWindowBoundaries_ShouldReturnExpectedResult(string instant, bool expected)
    {
        var sut = CreateWindow();

        sut.IsOutsideWindow(DateTimeOffset.Parse(instant)).Should().Be(expected);
    }

    [Fact]
    public void IsOutsideWindow_ForSaturdayHoliday_ShouldReturnTrue()
    {
        var sut = CreateWindow("2027-05-01");

        sut.IsOutsideWindow(DateTimeOffset.Parse("2027-05-01T15:00:00Z")).Should().BeTrue();
    }

    [Theory]
    [InlineData("2026-07-20T11:30:00Z", "2026-07-20T11:30:00Z")]
    [InlineData("2026-07-20T10:30:00Z", "2026-07-20T11:00:00Z")]
    [InlineData("2026-07-25T23:00:00Z", "2026-07-27T11:00:00Z")]
    [InlineData("2026-07-26T03:40:00Z", "2026-07-27T11:00:00Z")]
    public void NextWindowStart_ForDifferentInstants_ShouldReturnFirstAvailableInstant(string instant, string expected)
    {
        var sut = CreateWindow();

        sut.NextWindowStart(DateTimeOffset.Parse(instant)).Should().Be(DateTimeOffset.Parse(expected));
    }

    [Fact]
    public void AddBusinessHours_AcrossDayBoundary_ShouldExcludeClosedHours()
    {
        var sut = CreateWindow();

        var dueAt = sut.AddBusinessHours(DateTimeOffset.Parse("2026-07-24T22:00:00Z"), 4); // Friday 19:00 local

        dueAt.Should().Be(DateTimeOffset.Parse("2026-07-25T14:00:00Z")); // Saturday 11:00 local
    }

    [Fact]
    public void AddBusinessHours_FromSunday_ShouldStartOnMondayAndAddFourHours()
    {
        var sut = CreateWindow();
        var receivedAt = DateTimeOffset.Parse("2026-07-26T03:40:00Z");

        var slaStartsAt = sut.NextWindowStart(receivedAt);
        var slaDueAt = sut.AddBusinessHours(slaStartsAt, 4);

        slaStartsAt.Should().Be(DateTimeOffset.Parse("2026-07-27T11:00:00Z"));
        slaDueAt.Should().Be(DateTimeOffset.Parse("2026-07-27T15:00:00Z"));
    }

    [Theory]
    [InlineData("StartTime", "20:00")]
    [InlineData("ProcessingSlaBusinessHours", "3")]
    [InlineData("Holidays:1", "2026-01-01")]
    public void ResolveOptions_WithInvalidServiceWindowConfiguration_ShouldFailFast(string key, string value)
    {
        var values = ValidOptions();
        values[$"Inventory:InventoryServiceWindow:{key}"] = value;
        using var provider = CreateProvider(values);

        var act = () => provider.GetRequiredService<IOptions<InventoryServiceWindowOptions>>().Value;

        act.Should().Throw<OptionsValidationException>()
            .WithMessage("*Inventory service window requires*");
    }

    private static IInventoryServiceWindow CreateWindow(string? holiday = null)
    {
        var options = new InventoryServiceWindowOptions
        {
            Version = "test-v1",
            TimeZone = "America/Fortaleza",
            WorkingDays = ["Monday", "Tuesday", "Wednesday", "Thursday", "Friday", "Saturday"],
            StartTime = "08:00",
            EndTime = "20:00",
            ProcessingSlaBusinessHours = 4,
            Holidays = holiday is null ? [] : [holiday],
        };
        return new ConfiguredInventoryServiceWindow(Options.Create(options));
    }

    private static Dictionary<string, string?> ValidOptions() => new()
    {
        ["Inventory:InventoryServiceWindow:Version"] = "test-v1",
        ["Inventory:InventoryServiceWindow:TimeZone"] = "America/Fortaleza",
        ["Inventory:InventoryServiceWindow:WorkingDays:0"] = "Monday",
        ["Inventory:InventoryServiceWindow:StartTime"] = "08:00",
        ["Inventory:InventoryServiceWindow:EndTime"] = "20:00",
        ["Inventory:InventoryServiceWindow:ProcessingSlaBusinessHours"] = "4",
        ["Inventory:InventoryServiceWindow:Holidays:0"] = "2026-01-01",
    };

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
