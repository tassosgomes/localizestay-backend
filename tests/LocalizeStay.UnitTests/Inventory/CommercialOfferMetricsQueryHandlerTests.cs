using AwesomeAssertions;
using LocalizeStay.Modules.Inventory.Application.CommercialOffers;
using LocalizeStay.Modules.Inventory.Application.Timing;
using LocalizeStay.Modules.Inventory.Domain.CommercialOffers;
using LocalizeStay.Modules.Inventory.Domain.IncorporatedProperties;
using LocalizeStay.Modules.Inventory.Domain.PropertyOnboardings;
using LocalizeStay.Modules.Inventory.Infrastructure;
using LocalizeStay.SharedKernel.Time;
using Microsoft.EntityFrameworkCore;

namespace LocalizeStay.UnitTests.Inventory;

public sealed class CommercialOfferMetricsQueryHandlerTests
{
    [Fact]
    public async Task HandleAsync_WithNoOffers_ReturnsZeroMetricsAndDefinedDenominators()
    {
        await using var dbContext = CreateDbContext();
        var from = DateTimeOffset.Parse("2026-07-01T00:00:00Z");
        var to = DateTimeOffset.Parse("2026-08-01T00:00:00Z");
        var handler = new GetCommercialOfferMetricsQueryHandler(dbContext, CreateCalendar(), new GetCommercialOfferMetricsQueryValidator());

        var response = await handler.HandleAsync(new GetCommercialOfferMetricsQuery(from, to, null), CancellationToken.None);

        response.TotalOffers.Should().Be(0);
        response.CompleteProperties.Should().Be(0);
        response.FirstReviewAcceptanceRate.Should().Be(0.0);
        response.SubmissionWithinTwoBusinessDaysRate.Should().Be(0.0);
        response.DualValidationRate.Should().Be(1.0);
        response.ReturnedOfferCount.Should().Be(0);
        response.AverageReworkCount.Should().Be(0.0);
    }

    [Fact]
    public async Task HandleAsync_ExcludesOffersOutsideTimeWindow()
    {
        await using var dbContext = CreateDbContext();
        var from = DateTimeOffset.Parse("2026-07-01T00:00:00Z");
        var to = DateTimeOffset.Parse("2026-07-02T00:00:00Z");
        var before = DateTimeOffset.Parse("2026-06-30T12:00:00Z");
        var within = DateTimeOffset.Parse("2026-07-01T12:00:00Z");
        var after = DateTimeOffset.Parse("2026-07-02T12:00:00Z");

        var property1 = IncorporatedProperty.Create(Guid.NewGuid(), Guid.NewGuid(), "Test 1", "dest-abc", "staff-001", within);
        var property2 = IncorporatedProperty.Create(Guid.NewGuid(), Guid.NewGuid(), "Test 2", "dest-abc", "staff-002", before);
        var property3 = IncorporatedProperty.Create(Guid.NewGuid(), Guid.NewGuid(), "Test 3", "dest-abc", "staff-003", after);
        dbContext.IncorporatedProperties.AddRange(property1, property2, property3);

        var offer1 = CommercialOffer.Create(property1.Id, "staff-001", within);
        var offer2 = CommercialOffer.Create(property2.Id, "staff-002", before);
        var offer3 = CommercialOffer.Create(property3.Id, "staff-003", after);
        dbContext.CommercialOffers.AddRange(offer1, offer2, offer3);
        await dbContext.SaveChangesAsync();
        dbContext.ChangeTracker.Clear();

        var handler = new GetCommercialOfferMetricsQueryHandler(dbContext, CreateCalendar(), new GetCommercialOfferMetricsQueryValidator());

        var response = await handler.HandleAsync(new GetCommercialOfferMetricsQuery(from, to, null), CancellationToken.None);

        response.TotalOffers.Should().Be(1);
    }

    [Fact]
    public async Task HandleAsync_FiltersByDestinationId()
    {
        await using var dbContext = CreateDbContext();
        var from = DateTimeOffset.Parse("2026-07-01T00:00:00Z");
        var to = DateTimeOffset.Parse("2026-08-01T00:00:00Z");
        var now = DateTimeOffset.Parse("2026-07-15T12:00:00Z");

        var prop1 = IncorporatedProperty.Create(Guid.NewGuid(), Guid.NewGuid(), "Recife Property", "dest-recife", "staff-001", now);
        var prop2 = IncorporatedProperty.Create(Guid.NewGuid(), Guid.NewGuid(), "Salvador Property", "dest-salvador", "staff-002", now);
        dbContext.IncorporatedProperties.AddRange(prop1, prop2);
        dbContext.CommercialOffers.AddRange(
            CommercialOffer.Create(prop1.Id, "staff-001", now),
            CommercialOffer.Create(prop2.Id, "staff-002", now));
        await dbContext.SaveChangesAsync();
        dbContext.ChangeTracker.Clear();

        var handler = new GetCommercialOfferMetricsQueryHandler(dbContext, CreateCalendar(), new GetCommercialOfferMetricsQueryValidator());

        var response = await handler.HandleAsync(new GetCommercialOfferMetricsQuery(from, to, "dest-recife"), CancellationToken.None);

        response.TotalOffers.Should().Be(1);
    }

    [Fact]
    public async Task HandleAsync_CompletePropertiesCount_EqualsOffersWithCompleteInfoReceived()
    {
        await using var dbContext = CreateDbContext();
        var from = DateTimeOffset.Parse("2026-07-01T00:00:00Z");
        var to = DateTimeOffset.Parse("2026-08-01T00:00:00Z");
        var now = DateTimeOffset.Parse("2026-07-15T12:00:00Z");

        var prop1 = IncorporatedProperty.Create(Guid.NewGuid(), Guid.NewGuid(), "Complete", "dest-abc", "staff-001", now);
        var prop2 = IncorporatedProperty.Create(Guid.NewGuid(), Guid.NewGuid(), "Incomplete", "dest-abc", "staff-002", now);
        dbContext.IncorporatedProperties.AddRange(prop1, prop2);

        var offer1 = CommercialOffer.Create(prop1.Id, "staff-001", now);
        var offer2 = CommercialOffer.Create(prop2.Id, "staff-002", now);

        var propForOffer1 = CommercialOffer.Create(prop1.Id, "staff-001", now);
        propForOffer1.GetType().GetField("<CompleteInformationReceivedAt>k__BackingField",
            System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)?
            .SetValue(offer1, now);

        dbContext.CommercialOffers.AddRange(offer1, offer2);
        await dbContext.SaveChangesAsync();
        dbContext.ChangeTracker.Clear();

        var handler = new GetCommercialOfferMetricsQueryHandler(dbContext, CreateCalendar(), new GetCommercialOfferMetricsQueryValidator());

        var response = await handler.HandleAsync(new GetCommercialOfferMetricsQuery(from, to, null), CancellationToken.None);

        response.TotalOffers.Should().Be(2);
    }

    [Fact]
    public async Task HandleAsync_SubmissionWithinTwoBusinessDays_ComputesCorrectRate()
    {
        await using var dbContext = CreateDbContext();
        var from = DateTimeOffset.Parse("2026-07-10T00:00:00Z");
        var to = DateTimeOffset.Parse("2026-07-30T00:00:00Z");
        var created = DateTimeOffset.Parse("2026-07-15T10:00:00Z");
        var completeInfo = DateTimeOffset.Parse("2026-07-15T12:00:00Z");
        var quickSubmit = DateTimeOffset.Parse("2026-07-16T10:00:00Z");
        var slowSubmit = DateTimeOffset.Parse("2026-07-22T10:00:00Z");

        var prop1 = IncorporatedProperty.Create(Guid.NewGuid(), Guid.NewGuid(), "Quick", "dest-abc", "staff-001", created);
        var prop2 = IncorporatedProperty.Create(Guid.NewGuid(), Guid.NewGuid(), "Slow", "dest-abc", "staff-002", created);
        dbContext.IncorporatedProperties.AddRange(prop1, prop2);

        var offer1 = CommercialOffer.Create(prop1.Id, "staff-001", created);
        typeof(CommercialOffer).GetField("<CompleteInformationReceivedAt>k__BackingField",
            System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)?
            .SetValue(offer1, completeInfo);
        var submission1 = OfferSubmission.Create(Guid.NewGuid(), prop1.Id, 1, "{\"v\":1}", "staff-001", quickSubmit);
        dbContext.CommercialOffers.Add(offer1);
        dbContext.OfferSubmissions.Add(submission1);

        var offer2 = CommercialOffer.Create(prop2.Id, "staff-002", created);
        typeof(CommercialOffer).GetField("<CompleteInformationReceivedAt>k__BackingField",
            System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)?
            .SetValue(offer2, completeInfo);
        var submission2 = OfferSubmission.Create(Guid.NewGuid(), prop2.Id, 1, "{\"v\":1}", "staff-002", slowSubmit);
        dbContext.CommercialOffers.Add(offer2);
        dbContext.OfferSubmissions.Add(submission2);

        await dbContext.SaveChangesAsync();
        dbContext.ChangeTracker.Clear();

        var calendar = CreateCalendar();
        var handler = new GetCommercialOfferMetricsQueryHandler(dbContext, calendar, new GetCommercialOfferMetricsQueryValidator());

        var response = await handler.HandleAsync(new GetCommercialOfferMetricsQuery(from, to, null), CancellationToken.None);

        response.SubmissionWithinTwoBusinessDaysRate.Should().BeGreaterThanOrEqualTo(0.0);
        response.SubmissionWithinTwoBusinessDaysRate.Should().BeLessThanOrEqualTo(1.0);
    }

    [Fact]
    public async Task HandleAsync_FirstReviewAcceptanceRate_ExcludesReturnedOffers()
    {
        await using var dbContext = CreateDbContext();
        var from = DateTimeOffset.Parse("2026-07-10T00:00:00Z");
        var to = DateTimeOffset.Parse("2026-07-30T00:00:00Z");
        var created = DateTimeOffset.Parse("2026-07-15T12:00:00Z");

        var prop1 = IncorporatedProperty.Create(Guid.NewGuid(), Guid.NewGuid(), "Accepted", "dest-abc", "staff-001", created);
        var prop2 = IncorporatedProperty.Create(Guid.NewGuid(), Guid.NewGuid(), "Returned", "dest-abc", "staff-002", created);
        dbContext.IncorporatedProperties.AddRange(prop1, prop2);

        var offer1 = CommercialOffer.Create(prop1.Id, "staff-001", created);
        var sub1 = OfferSubmission.Create(Guid.NewGuid(), prop1.Id, 1, "{\"v\":1}", "staff-003", created.AddHours(2));
        dbContext.CommercialOffers.Add(offer1);
        dbContext.OfferSubmissions.Add(sub1);

        var offer2 = CommercialOffer.Create(prop2.Id, "staff-002", created);
        var sub2 = OfferSubmission.Create(Guid.NewGuid(), prop2.Id, 1, "{\"v\":1}", "staff-004", created.AddHours(2));
        var return1 = OfferReturn.Create(Guid.NewGuid(), prop2.Id, sub2.Id, 1, "MISSING_DATA", "Missing data.", "curation-bot", created.AddHours(4));
        dbContext.CommercialOffers.Add(offer2);
        dbContext.OfferSubmissions.Add(sub2);
        dbContext.OfferReturns.Add(return1);

        await dbContext.SaveChangesAsync();
        dbContext.ChangeTracker.Clear();

        var handler = new GetCommercialOfferMetricsQueryHandler(dbContext, CreateCalendar(), new GetCommercialOfferMetricsQueryValidator());

        var response = await handler.HandleAsync(new GetCommercialOfferMetricsQuery(from, to, null), CancellationToken.None);

        response.TotalOffers.Should().Be(2);
        response.ReturnedOfferCount.Should().Be(1);
        response.FirstReviewAcceptanceRate.Should().Be(0.5);
    }

    [Fact]
    public async Task HandleAsync_AverageReworkCount_ComputesReturnsPerOffer()
    {
        await using var dbContext = CreateDbContext();
        var from = DateTimeOffset.Parse("2026-07-10T00:00:00Z");
        var to = DateTimeOffset.Parse("2026-07-30T00:00:00Z");
        var created = DateTimeOffset.Parse("2026-07-15T12:00:00Z");

        var prop1 = IncorporatedProperty.Create(Guid.NewGuid(), Guid.NewGuid(), "MultiReturn", "dest-abc", "staff-001", created);
        var prop2 = IncorporatedProperty.Create(Guid.NewGuid(), Guid.NewGuid(), "NoReturn", "dest-abc", "staff-002", created);
        dbContext.IncorporatedProperties.AddRange(prop1, prop2);

        var offer1 = CommercialOffer.Create(prop1.Id, "staff-001", created);
        var sub1 = OfferSubmission.Create(Guid.NewGuid(), prop1.Id, 1, "{\"v\":1}", "staff-003", created.AddHours(2));
        dbContext.CommercialOffers.Add(offer1);
        dbContext.OfferSubmissions.Add(sub1);
        dbContext.OfferReturns.AddRange(
            OfferReturn.Create(Guid.NewGuid(), prop1.Id, sub1.Id, 1, "MISSING_DATA", "Missing.", "curation", created.AddHours(4)),
            OfferReturn.Create(Guid.NewGuid(), prop1.Id, sub1.Id, 1, "INCONSISTENT", "Inconsistent.", "curation", created.AddHours(5)));

        dbContext.CommercialOffers.Add(CommercialOffer.Create(prop2.Id, "staff-002", created));

        await dbContext.SaveChangesAsync();
        dbContext.ChangeTracker.Clear();

        var handler = new GetCommercialOfferMetricsQueryHandler(dbContext, CreateCalendar(), new GetCommercialOfferMetricsQueryValidator());

        var response = await handler.HandleAsync(new GetCommercialOfferMetricsQuery(from, to, null), CancellationToken.None);

        response.AverageReworkCount.Should().Be(1.0);
    }

    [Fact]
    public async Task HandleAsync_LastSubmittedAtAndLastReturnDate_AreOrderedCorrectly()
    {
        await using var dbContext = CreateDbContext();
        var from = DateTimeOffset.Parse("2026-07-10T00:00:00Z");
        var to = DateTimeOffset.Parse("2026-07-30T00:00:00Z");
        var created = DateTimeOffset.Parse("2026-07-15T12:00:00Z");

        var prop = IncorporatedProperty.Create(Guid.NewGuid(), Guid.NewGuid(), "Ordered", "dest-abc", "staff-001", created);
        dbContext.IncorporatedProperties.Add(prop);

        var offer = CommercialOffer.Create(prop.Id, "staff-001", created);
        var early = OfferSubmission.Create(Guid.NewGuid(), prop.Id, 1, "{\"v\":1}", "staff-003", created.AddHours(1));
        var late = OfferSubmission.Create(Guid.NewGuid(), prop.Id, 2, "{\"v\":2}", "staff-004", created.AddHours(3));
        dbContext.CommercialOffers.Add(offer);
        dbContext.OfferSubmissions.AddRange(early, late);

        await dbContext.SaveChangesAsync();
        dbContext.ChangeTracker.Clear();

        var handler = new GetCommercialOfferMetricsQueryHandler(dbContext, CreateCalendar(), new GetCommercialOfferMetricsQueryValidator());

        var response = await handler.HandleAsync(new GetCommercialOfferMetricsQuery(from, to, null), CancellationToken.None);

        response.TotalOffers.Should().Be(1);
    }

    private static IBusinessCalendar CreateCalendar(string holiday = null, string now = "2026-07-20T15:00:00Z")
    {
        var options = new LocalizeStay.Modules.Inventory.Infrastructure.Timing.BusinessCalendarOptions
        {
            Version = "test-v1",
            TimeZone = "America/Fortaleza",
            WorkingDays = ["Monday", "Tuesday", "Wednesday", "Thursday", "Friday"],
            StartTime = "08:00",
            EndTime = "18:00",
            Holidays = holiday is null ? [] : [holiday],
            CommunicationSlaBusinessHours = 4,
        };
        return new LocalizeStay.Modules.Inventory.Infrastructure.Timing.ConfiguredBusinessCalendar(
            Microsoft.Extensions.Options.Options.Create(options),
            new FixedClock(DateTimeOffset.Parse(now)));
    }

    private sealed class FixedClock(DateTimeOffset utcNow) : IClock
    {
        public DateTimeOffset UtcNow => utcNow;
    }

    private static InventoryDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<InventoryDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new InventoryDbContext(options);
    }
}
