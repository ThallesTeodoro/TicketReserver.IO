namespace TR.API.Events.UnitTests.Application.Events;

using FluentAssertions;
using Moq;
using TR.API.Events.Application.Events.CreateEvent;
using TR.API.Events.Application.Events.UpdateEvent;
using TR.API.Events.Domain.Contracts.Persistence;
using TR.API.Events.Domain.Entities;
using TR.API.Events.Domain.Exceptions;
using TR.API.Events.Domain.ValueObjects;
using Xunit;

public class CreateAndUpdateEventHandlerTests
{
    private readonly Mock<IEventRepository> _eventRepositoryMock = new();
    private readonly Mock<IUnitOfWork> _unitOfWorkMock = new();

    [Fact]
    public async Task CreateEvent_WithValidCommand_ShouldPersistAndReturnResponse()
    {
        // Arrange
        var venueId = Guid.NewGuid();
        var now = DateTimeOffset.UtcNow;
        var command = new CreateEventCommand(
            venueId,
            "Lollapalooza Brasil",
            "lollapalooza-br-2026",
            "Festival internacional",
            "FESTIVAL",
            now.AddDays(1),
            now.AddDays(10),
            now.AddDays(20),
            now.AddDays(22),
            16,
            "https://img.url/lolla.jpg",
            4);

        _eventRepositoryMock
            .Setup(r => r.VenueExistsAsync(venueId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        _eventRepositoryMock
            .Setup(r => r.ExistsBySlugAsync(command.Slug, null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        var handler = new CreateEventCommandHandler(
            _eventRepositoryMock.Object,
            _unitOfWorkMock.Object,
            new CreateEventCommandValidator());

        // Act
        var result = await handler.HandleAsync(command);

        // Assert
        result.Should().NotBeNull();
        result.Title.Should().Be("Lollapalooza Brasil");
        result.Slug.Should().Be("lollapalooza-br-2026");
        result.Status.Should().Be("DRAFT");
        result.Version.Should().Be(1);

        _eventRepositoryMock.Verify(r => r.AddAsync(It.IsAny<Event>(), It.IsAny<CancellationToken>()), Times.Once);
        _unitOfWorkMock.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task CreateEvent_WhenVenueDoesNotExist_ShouldThrowNotFoundException()
    {
        // Arrange
        var venueId = Guid.NewGuid();
        var now = DateTimeOffset.UtcNow;
        var command = new CreateEventCommand(
            venueId,
            "Show",
            "show-slug",
            null,
            "CONCERT",
            now.AddDays(1),
            now.AddDays(5),
            now.AddDays(10),
            now.AddDays(11));

        _eventRepositoryMock
            .Setup(r => r.VenueExistsAsync(venueId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        var handler = new CreateEventCommandHandler(
            _eventRepositoryMock.Object,
            _unitOfWorkMock.Object,
            new CreateEventCommandValidator());

        // Act
        var act = async () => await handler.HandleAsync(command);

        // Assert
        await act.Should().ThrowAsync<NotFoundException>()
            .WithMessage($"*{venueId}*");
    }

    [Fact]
    public async Task UpdateEvent_WithValidCommand_ShouldUpdateAndIncrementVersion()
    {
        // Arrange
        var venueId = Guid.NewGuid();
        var now = DateTimeOffset.UtcNow;
        var existingEvent = Event.Create(
            venueId,
            "Original Title",
            "original-slug",
            "Desc",
            "CONCERT",
            new DateTimeRange(now.AddDays(1), now.AddDays(5)),
            new DateTimeRange(now.AddDays(10), now.AddDays(11)));

        var command = new UpdateEventCommand(
            existingEvent.Id,
            venueId,
            "Updated Title",
            "updated-slug",
            "Updated Desc",
            "FESTIVAL",
            now.AddDays(2),
            now.AddDays(6),
            now.AddDays(12),
            now.AddDays(13),
            18,
            "https://banner.url",
            6);

        _eventRepositoryMock
            .Setup(r => r.GetByIdAsync(existingEvent.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(existingEvent);

        _eventRepositoryMock
            .Setup(r => r.VenueExistsAsync(venueId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        _eventRepositoryMock
            .Setup(r => r.ExistsBySlugAsync(command.Slug, existingEvent.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        var handler = new UpdateEventCommandHandler(
            _eventRepositoryMock.Object,
            _unitOfWorkMock.Object,
            new UpdateEventCommandValidator());

        // Act
        var result = await handler.HandleAsync(command);

        // Assert
        result.Title.Should().Be("Updated Title");
        result.Slug.Should().Be("updated-slug");
        result.Category.Should().Be("FESTIVAL");
        result.Version.Should().Be(2);

        _eventRepositoryMock.Verify(r => r.Update(existingEvent), Times.Once);
        _unitOfWorkMock.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }
}
