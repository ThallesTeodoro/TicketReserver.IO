namespace TR.API.Events.UnitTests.Application.Events;

using FluentAssertions;
using Moq;
using TR.API.Events.Application.Events.CancelEvent;
using TR.API.Events.Application.Events.ChangeEventStatus;
using TR.API.Events.Domain.Contracts.Persistence;
using TR.API.Events.Domain.Entities;
using TR.API.Events.Domain.Exceptions;
using TR.API.Events.Domain.ValueObjects;
using Xunit;

public class EventLifecycleHandlerTests
{
    private readonly Mock<IEventRepository> _eventRepositoryMock = new();
    private readonly Mock<IUnitOfWork> _unitOfWorkMock = new();

    private static Event CreateDraftEvent()
    {
        var now = DateTimeOffset.UtcNow;
        return Event.Create(
            Guid.NewGuid(),
            "Festival",
            "festival-slug",
            "Desc",
            "FESTIVAL",
            new DateTimeRange(now.AddDays(1), now.AddDays(5)),
            new DateTimeRange(now.AddDays(10), now.AddDays(11)));
    }

    [Fact]
    public async Task ChangeEventStatus_FromDraftToPublished_ShouldSucceed()
    {
        // Arrange
        var evt = CreateDraftEvent();
        _eventRepositoryMock
            .Setup(r => r.GetByIdAsync(evt.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(evt);

        var handler = new ChangeEventStatusCommandHandler(
            _eventRepositoryMock.Object,
            _unitOfWorkMock.Object,
            new ChangeEventStatusCommandValidator());

        // Act
        var result = await handler.HandleAsync(new ChangeEventStatusCommand(evt.Id, "PUBLISHED"));

        // Assert
        result.Status.Should().Be("PUBLISHED");
        result.Version.Should().Be(2);
        _unitOfWorkMock.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ChangeEventStatus_WhenInvalidTransition_ShouldThrowDomainException()
    {
        // Arrange
        var evt = CreateDraftEvent();
        _eventRepositoryMock
            .Setup(r => r.GetByIdAsync(evt.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(evt);

        var handler = new ChangeEventStatusCommandHandler(
            _eventRepositoryMock.Object,
            _unitOfWorkMock.Object,
            new ChangeEventStatusCommandValidator());

        // Act
        var act = async () => await handler.HandleAsync(new ChangeEventStatusCommand(evt.Id, "SOLD_OUT"));

        // Assert
        await act.Should().ThrowAsync<DomainException>();
    }

    [Fact]
    public async Task CancelEvent_ShouldTransitionStatusToCancelled()
    {
        // Arrange
        var evt = CreateDraftEvent();
        _eventRepositoryMock
            .Setup(r => r.GetByIdAsync(evt.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(evt);

        var handler = new CancelEventCommandHandler(
            _eventRepositoryMock.Object,
            _unitOfWorkMock.Object);

        // Act
        var result = await handler.HandleAsync(new CancelEventCommand(evt.Id));

        // Assert
        result.Status.Should().Be("CANCELLED");
        result.Version.Should().Be(2);
        _unitOfWorkMock.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }
}
