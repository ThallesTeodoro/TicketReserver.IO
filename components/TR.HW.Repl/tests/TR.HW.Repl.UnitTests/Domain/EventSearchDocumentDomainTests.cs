using FluentAssertions;
using TR.HW.Repl.Domain.Entities;
using TR.HW.Repl.Domain.Exceptions;
using TR.HW.Repl.Domain.ValueObjects;

namespace TR.HW.Repl.UnitTests.Domain;

public class EventSearchDocumentDomainTests
{
    private static (EventScheduleInfo Schedule, VenueSearchInfo Venue) CreateDefaults()
    {
        var now = DateTimeOffset.UtcNow;
        var schedule = new EventScheduleInfo(
            SalesStartAt: now,
            SalesEndAt: now.AddDays(10),
            EventStartAt: now.AddDays(15),
            EventEndAt: now.AddDays(15).AddHours(3));

        var venue = new VenueSearchInfo(
            Id: Guid.NewGuid(),
            Name: "Allianz Parque",
            City: "São Paulo",
            State: "SP",
            Country: "BR",
            Location: new GeoLocation(-23.5275, -46.6784));

        return (schedule, venue);
    }

    [Fact]
    public void Create_WithValidParameters_ShouldInstantiateDocumentWithExpectedValues()
    {
        // Arrange
        var id = Guid.NewGuid();
        var (schedule, venue) = CreateDefaults();

        // Act
        var doc = EventSearchDocument.Create(
            id: id,
            title: "Rock in Rio 2026",
            slug: "rock-in-rio-2026",
            description: "Maior festival de música do mundo",
            category: "FESTIVAL",
            status: "SALES_OPEN",
            ageRestriction: 16,
            bannerUrl: "https://cdn.ticketreserver.io/rir.jpg",
            schedule: schedule,
            venue: venue,
            tags: ["rock", "festival", "rio"]);

        // Assert
        doc.Should().NotBeNull();
        doc.Id.Should().Be(id);
        doc.Title.Should().Be("Rock in Rio 2026");
        doc.Slug.Should().Be("rock-in-rio-2026");
        doc.Category.Should().Be("FESTIVAL");
        doc.Status.Should().Be("SALES_OPEN");
        doc.AgeRestriction.Should().Be(16);
        doc.BannerUrl.Should().Be("https://cdn.ticketreserver.io/rir.jpg");
        doc.Tags.Should().Contain(["rock", "festival", "rio"]);
        doc.Pricing.MinPrice.Should().Be(0);
        doc.Pricing.MaxPrice.Should().Be(0);
        doc.TiersSummary.Should().BeEmpty();
    }

    [Fact]
    public void UpsertTier_ShouldAddTiersAndRecalculateMinAndMaxPrices()
    {
        // Arrange
        var (schedule, venue) = CreateDefaults();
        var doc = EventSearchDocument.Create(
            Guid.NewGuid(), "Show", "show", "Desc", "CONCERT", "ACTIVE", 18, null, schedule, venue);

        var tier1 = TicketTierSummary.Create(Guid.NewGuid(), "Pista", 200m);
        var tier2 = TicketTierSummary.Create(Guid.NewGuid(), "Premium", 450m);
        var tier3 = TicketTierSummary.Create(Guid.NewGuid(), "VIP", 800m);

        // Act
        doc.UpsertTier(tier1);
        doc.UpsertTier(tier2);
        doc.UpsertTier(tier3);

        // Assert
        doc.TiersSummary.Should().HaveCount(3);
        doc.Pricing.MinPrice.Should().Be(200m);
        doc.Pricing.MaxPrice.Should().Be(800m);
    }

    [Fact]
    public void UpsertTier_WhenTierAlreadyExists_ShouldUpdateExistingWithoutDuplicating()
    {
        // Arrange
        var (schedule, venue) = CreateDefaults();
        var doc = EventSearchDocument.Create(
            Guid.NewGuid(), "Show", "show", "Desc", "CONCERT", "ACTIVE", 18, null, schedule, venue);

        var tierId = Guid.NewGuid();
        var tierLote1 = TicketTierSummary.Create(tierId, "Pista - 1º Lote", 150m);
        var tierLote2 = TicketTierSummary.Create(tierId, "Pista - 2º Lote", 220m);

        // Act
        doc.UpsertTier(tierLote1);
        doc.UpsertTier(tierLote2);

        // Assert
        doc.TiersSummary.Should().HaveCount(1);
        doc.TiersSummary.First().Name.Should().Be("Pista - 2º Lote");
        doc.Pricing.MinPrice.Should().Be(220m);
        doc.Pricing.MaxPrice.Should().Be(220m);
    }

    [Fact]
    public void RemoveTier_ShouldRemoveTierAndRecalculatePrices()
    {
        // Arrange
        var (schedule, venue) = CreateDefaults();
        var doc = EventSearchDocument.Create(
            Guid.NewGuid(), "Show", "show", "Desc", "CONCERT", "ACTIVE", 18, null, schedule, venue);

        var tier1 = TicketTierSummary.Create(Guid.NewGuid(), "Pista", 100m);
        var tier2 = TicketTierSummary.Create(Guid.NewGuid(), "Premium", 500m);
        doc.UpsertTier(tier1);
        doc.UpsertTier(tier2);

        // Act
        doc.RemoveTier(tier1.TierId);

        // Assert
        doc.TiersSummary.Should().HaveCount(1);
        doc.Pricing.MinPrice.Should().Be(500m);
        doc.Pricing.MaxPrice.Should().Be(500m);
    }

    [Fact]
    public void MarkAsCancelled_ShouldUpdateDocumentStatus()
    {
        // Arrange
        var (schedule, venue) = CreateDefaults();
        var doc = EventSearchDocument.Create(
            Guid.NewGuid(), "Show", "show", "Desc", "CONCERT", "ACTIVE", 18, null, schedule, venue);

        // Act
        doc.MarkAsCancelled();

        // Assert
        doc.Status.Should().Be("CANCELLED");
    }

    [Fact]
    public void Create_WithInvalidArguments_ShouldThrowDomainException()
    {
        // Arrange
        var (schedule, venue) = CreateDefaults();

        // Act & Assert
        var actId = () => EventSearchDocument.Create(Guid.Empty, "T", "S", "D", "C", "S", 0, null, schedule, venue);
        var actTitle = () => EventSearchDocument.Create(Guid.NewGuid(), "", "S", "D", "C", "S", 0, null, schedule, venue);
        var actSlug = () => EventSearchDocument.Create(Guid.NewGuid(), "T", "", "D", "C", "S", 0, null, schedule, venue);
        var actSchedule = () => EventSearchDocument.Create(Guid.NewGuid(), "T", "S", "D", "C", "S", 0, null, null!, venue);
        var actVenue = () => EventSearchDocument.Create(Guid.NewGuid(), "T", "S", "D", "C", "S", 0, null, schedule, null!);

        actId.Should().Throw<DomainException>().WithMessage("*identifier cannot be empty*");
        actTitle.Should().Throw<DomainException>().WithMessage("*title cannot be empty*");
        actSlug.Should().Throw<DomainException>().WithMessage("*slug cannot be empty*");
        actSchedule.Should().Throw<DomainException>().WithMessage("*Schedule must be specified*");
        actVenue.Should().Throw<DomainException>().WithMessage("*Venue must be specified*");
    }

    [Fact]
    public void TicketTierSummary_WithNegativePrice_ShouldThrowDomainException()
    {
        // Act
        var act = () => TicketTierSummary.Create(Guid.NewGuid(), "Tier", -10m);

        // Assert
        act.Should().Throw<DomainException>().WithMessage("*price cannot be negative*");
    }
}
