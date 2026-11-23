using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using TR.HW.Repl.Application.Features.Replication;
using TR.HW.Repl.Application.Interfaces;
using TR.HW.Repl.Domain.Entities;
using TR.HW.Repl.Domain.Models;

namespace TR.HW.Repl.UnitTests.Application;

public class ReplicationCommandHandlersTests
{
    private readonly Mock<IEventSearchRepository> _repositoryMock;
    private readonly Mock<ILogger<ReplicateEventChangeCommandHandler>> _eventLoggerMock;
    private readonly Mock<ILogger<ReplicateTicketTierChangeCommandHandler>> _tierLoggerMock;
    private readonly ReplicateEventChangeCommandHandler _eventHandler;
    private readonly ReplicateTicketTierChangeCommandHandler _tierHandler;

    public ReplicationCommandHandlersTests()
    {
        _repositoryMock = new Mock<IEventSearchRepository>();
        _eventLoggerMock = new Mock<ILogger<ReplicateEventChangeCommandHandler>>();
        _tierLoggerMock = new Mock<ILogger<ReplicateTicketTierChangeCommandHandler>>();

        _eventHandler = new ReplicateEventChangeCommandHandler(
            _repositoryMock.Object,
            new ReplicateEventChangeCommandValidator(),
            _eventLoggerMock.Object);

        _tierHandler = new ReplicateTicketTierChangeCommandHandler(
            _repositoryMock.Object,
            new ReplicateTicketTierChangeCommandValidator(),
            _tierLoggerMock.Object);
    }

    [Fact]
    public async Task HandleAsync_ForEventInsert_ShouldUpsertDocument()
    {
        // Arrange
        var eventId = Guid.NewGuid();
        var command = new ReplicateEventChangeCommand(
            EventId: eventId,
            Operation: CdcOperation.Create,
            Title: "Lollapalooza 2026",
            Slug: "lollapalooza-2026",
            Description: "Festival em Interlagos",
            Category: "FESTIVAL",
            Status: "SALES_OPEN",
            VenueId: Guid.NewGuid(),
            VenueName: "Autódromo de Interlagos",
            VenueCity: "São Paulo",
            VenueState: "SP",
            VenueCountry: "BR",
            VenueLatitude: -23.7011,
            VenueLongitude: -46.6972);

        _repositoryMock
            .Setup(r => r.GetEventByIdAsync(eventId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((EventSearchDocument?)null);

        // Act
        var result = await _eventHandler.HandleAsync(command);

        // Assert
        result.Success.Should().BeTrue();
        _repositoryMock.Verify(r => r.UpsertEventAsync(
            It.Is<EventSearchDocument>(d => d.Id == eventId && d.Title == "Lollapalooza 2026"),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task HandleAsync_ForEventDelete_ShouldCallDeleteEventAsync()
    {
        // Arrange
        var eventId = Guid.NewGuid();
        var command = new ReplicateEventChangeCommand(
            EventId: eventId,
            Operation: CdcOperation.Delete);

        // Act
        var result = await _eventHandler.HandleAsync(command);

        // Assert
        result.Success.Should().BeTrue();
        _repositoryMock.Verify(r => r.DeleteEventAsync(eventId, It.IsAny<CancellationToken>()), Times.Once);
        _repositoryMock.Verify(r => r.UpsertEventAsync(It.IsAny<EventSearchDocument>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task HandleAsync_WhenStatusIsCancelled_ShouldCallUpdateEventStatusAsync()
    {
        // Arrange
        var eventId = Guid.NewGuid();
        var command = new ReplicateEventChangeCommand(
            EventId: eventId,
            Operation: CdcOperation.Update,
            Title: "Show Cancelado",
            Slug: "show-cancelado",
            Category: "CONCERT",
            Status: "CANCELLED");

        // Act
        var result = await _eventHandler.HandleAsync(command);

        // Assert
        result.Success.Should().BeTrue();
        _repositoryMock.Verify(r => r.UpdateEventStatusAsync(eventId, "CANCELLED", It.IsAny<DateTimeOffset>(), It.IsAny<CancellationToken>()), Times.Once);
        _repositoryMock.Verify(r => r.UpsertEventAsync(It.IsAny<EventSearchDocument>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task HandleAsync_ForEventWithInvalidData_ShouldReturnValidationFailure()
    {
        // Arrange
        var command = new ReplicateEventChangeCommand(
            EventId: Guid.Empty,
            Operation: CdcOperation.Create,
            Title: "");

        // Act
        var result = await _eventHandler.HandleAsync(command);

        // Assert
        result.Success.Should().BeFalse();
        result.ErrorMessage.Should().Contain("EventId is required");
        result.ErrorMessage.Should().Contain("Title is required");
        _repositoryMock.Verify(r => r.UpsertEventAsync(It.IsAny<EventSearchDocument>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task HandleAsync_ForTicketTierUpsert_ShouldCallUpdateTierPricingAsync()
    {
        // Arrange
        var eventId = Guid.NewGuid();
        var tierId = Guid.NewGuid();
        var command = new ReplicateTicketTierChangeCommand(
            TierId: tierId,
            EventId: eventId,
            Operation: CdcOperation.Create,
            TierName: "Pista Premium",
            Price: 350.00m,
            Status: "ACTIVE");

        // Act
        var result = await _tierHandler.HandleAsync(command);

        // Assert
        result.Success.Should().BeTrue();
        _repositoryMock.Verify(r => r.UpdateTierPricingAsync(
            eventId,
            It.Is<TicketTierSummary>(t => t.TierId == tierId && t.Price == 350.00m),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task HandleAsync_ForTicketTierDelete_ShouldCallRemoveTierPricingAsync()
    {
        // Arrange
        var eventId = Guid.NewGuid();
        var tierId = Guid.NewGuid();
        var command = new ReplicateTicketTierChangeCommand(
            TierId: tierId,
            EventId: eventId,
            Operation: CdcOperation.Delete);

        // Act
        var result = await _tierHandler.HandleAsync(command);

        // Assert
        result.Success.Should().BeTrue();
        _repositoryMock.Verify(r => r.RemoveTierPricingAsync(eventId, tierId, It.IsAny<CancellationToken>()), Times.Once);
        _repositoryMock.Verify(r => r.UpdateTierPricingAsync(It.IsAny<Guid>(), It.IsAny<TicketTierSummary>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task HandleAsync_ForTicketTierWithNegativePrice_ShouldReturnValidationFailure()
    {
        // Arrange
        var command = new ReplicateTicketTierChangeCommand(
            TierId: Guid.NewGuid(),
            EventId: Guid.NewGuid(),
            Operation: CdcOperation.Create,
            TierName: "Tier Inválido",
            Price: -50.00m);

        // Act
        var result = await _tierHandler.HandleAsync(command);

        // Assert
        result.Success.Should().BeFalse();
        result.ErrorMessage.Should().Contain("non-negative Price is required");
        _repositoryMock.Verify(r => r.UpdateTierPricingAsync(It.IsAny<Guid>(), It.IsAny<TicketTierSummary>(), It.IsAny<CancellationToken>()), Times.Never);
    }
}
