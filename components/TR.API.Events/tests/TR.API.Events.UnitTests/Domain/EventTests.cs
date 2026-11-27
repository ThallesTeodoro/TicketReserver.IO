namespace TR.API.Events.UnitTests.Domain;

using FluentAssertions;
using TR.API.Events.Domain.Entities;
using TR.API.Events.Domain.Enums;
using TR.API.Events.Domain.Exceptions;
using TR.API.Events.Domain.ValueObjects;
using Xunit;

public class EventTests
{
    private static (DateTimeRange SalesWindow, DateTimeRange Schedule) CreateValidWindows()
    {
        var now = DateTimeOffset.UtcNow;
        var salesWindow = new DateTimeRange(now.AddDays(1), now.AddDays(10), "SalesWindow");
        var schedule = new DateTimeRange(now.AddDays(15), now.AddDays(15).AddHours(4), "Schedule");
        return (salesWindow, schedule);
    }

    [Fact]
    public void Create_WithValidParameters_ShouldInitializeEventInDraftStatus()
    {
        // Arrange
        var venueId = Guid.NewGuid();
        var (salesWindow, schedule) = CreateValidWindows();

        // Act
        var evt = Event.Create(
            venueId,
            "Rock in Rio",
            "rock-in-rio-2026",
            "Festival de musica",
            "Festival",
            salesWindow,
            schedule,
            16,
            "https://banner.url/img.png",
            4);

        // Assert
        evt.Id.Should().NotBeEmpty();
        evt.VenueId.Should().Be(venueId);
        evt.Title.Should().Be("Rock in Rio");
        evt.Slug.Should().Be("rock-in-rio-2026");
        evt.Category.Should().Be("FESTIVAL");
        evt.Status.Should().Be(EventStatus.DRAFT);
        evt.Version.Should().Be(1);
        evt.TicketTiers.Should().BeEmpty();
    }

    [Fact]
    public void Create_WhenEventStartsBeforeSalesStart_ShouldThrowDomainException()
    {
        // Arrange
        var now = DateTimeOffset.UtcNow;
        var salesWindow = new DateTimeRange(now.AddDays(10), now.AddDays(20));
        var schedule = new DateTimeRange(now.AddDays(5), now.AddDays(6));

        // Act
        var act = () => Event.Create(
            Guid.NewGuid(),
            "Invalid Show",
            "invalid-show",
            null,
            "CONCERT",
            salesWindow,
            schedule);

        // Assert
        act.Should().Throw<DomainException>()
            .WithMessage("*Event start date must be on or after the sales start date*");
    }

    [Fact]
    public void DateTimeRange_WhenEndIsBeforeOrEqualToStart_ShouldThrowDomainException()
    {
        // Arrange
        var now = DateTimeOffset.UtcNow;

        // Act
        var act = () => new DateTimeRange(now, now.AddHours(-1));

        // Assert
        act.Should().Throw<DomainException>();
    }

    [Fact]
    public void LifecycleTransitions_WhenFollowingValidSequence_ShouldUpdateStatusAndIncrementVersion()
    {
        // Arrange
        var (salesWindow, schedule) = CreateValidWindows();
        var evt = Event.Create(Guid.NewGuid(), "Show", "show-slug", null, "CONCERT", salesWindow, schedule);

        // Act & Assert
        evt.Publish();
        evt.Status.Should().Be(EventStatus.PUBLISHED);
        evt.Version.Should().Be(2);

        evt.OpenSales();
        evt.Status.Should().Be(EventStatus.SALES_OPEN);
        evt.Version.Should().Be(3);

        evt.PauseSales();
        evt.Status.Should().Be(EventStatus.SALES_PAUSED);
        evt.Version.Should().Be(4);

        evt.OpenSales();
        evt.Status.Should().Be(EventStatus.SALES_OPEN);
        evt.Version.Should().Be(5);

        evt.MarkAsSoldOut();
        evt.Status.Should().Be(EventStatus.SOLD_OUT);
        evt.Version.Should().Be(6);

        evt.Cancel();
        evt.Status.Should().Be(EventStatus.CANCELLED);
        evt.Version.Should().Be(7);
    }

    [Fact]
    public void OpenSales_WhenEventIsDraft_ShouldThrowDomainException()
    {
        // Arrange
        var (salesWindow, schedule) = CreateValidWindows();
        var evt = Event.Create(Guid.NewGuid(), "Show", "show-slug", null, "CONCERT", salesWindow, schedule);

        // Act
        var act = () => evt.OpenSales();

        // Assert
        act.Should().Throw<DomainException>()
            .WithMessage("*Cannot open sales from status 'DRAFT'*");
    }

    [Fact]
    public void UpdateDetails_WhenEventIsCancelled_ShouldThrowDomainException()
    {
        // Arrange
        var (salesWindow, schedule) = CreateValidWindows();
        var evt = Event.Create(Guid.NewGuid(), "Show", "show-slug", null, "CONCERT", salesWindow, schedule);
        evt.Cancel();

        // Act
        var act = () => evt.UpdateDetails(
            evt.VenueId,
            "Updated Show",
            "updated-slug",
            "Desc",
            "CONCERT",
            salesWindow,
            schedule,
            18,
            null,
            4);

        // Assert
        act.Should().Throw<DomainException>()
            .WithMessage("*Cannot update an event in status 'CANCELLED'*");
    }
}
