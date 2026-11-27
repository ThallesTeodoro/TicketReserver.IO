namespace TR.API.Reservation.UnitTests.Domain;

using FluentAssertions;
using TR.API.Reservation.Domain.Entities;
using TR.API.Reservation.Domain.Enums;
using TR.API.Reservation.Domain.Exceptions;

public class OutboxMessageTests
{
    [Fact]
    public void Create_WithValidParameters_ShouldInstantiatePendingOutboxMessage()
    {
        // Arrange
        var correlationId = Guid.NewGuid();
        var aggregateId = Guid.NewGuid();

        // Act
        var outbox = OutboxMessage.Create(correlationId, "reservation.hold_created", "Reservation", aggregateId, "{\"id\":1}", 0);

        // Assert
        outbox.Should().NotBeNull();
        outbox.Id.Should().NotBeEmpty();
        outbox.CorrelationId.Should().Be(correlationId);
        outbox.EventType.Should().Be("reservation.hold_created");
        outbox.AggregateType.Should().Be("Reservation");
        outbox.AggregateId.Should().Be(aggregateId);
        outbox.Status.Should().Be(OutboxStatus.PENDING);
        outbox.RetryCount.Should().Be(0);
        outbox.ErrorMessage.Should().BeNull();
    }

    [Fact]
    public void Create_WithEmptyEventType_ShouldThrowDomainException()
    {
        // Act
        var act = () => OutboxMessage.Create(Guid.NewGuid(), "", "Reservation", Guid.NewGuid(), "{}");

        // Assert
        act.Should().Throw<DomainException>().WithMessage("*EventType não pode ser vazio*");
    }

    [Fact]
    public void MarkAsPublished_ShouldSetStatusToPublishedAndProcessedAt()
    {
        // Arrange
        var outbox = OutboxMessage.Create(Guid.NewGuid(), "reservation.hold_created", "Reservation", Guid.NewGuid(), "{}");
        var now = DateTime.UtcNow;

        // Act
        outbox.MarkAsPublished(now);

        // Assert
        outbox.Status.Should().Be(OutboxStatus.PUBLISHED);
        outbox.ProcessedAt.Should().Be(now);
        outbox.ErrorMessage.Should().BeNull();
    }

    [Fact]
    public void MarkAsFailed_ShouldSetStatusToFailedAndErrorMessage()
    {
        // Arrange
        var outbox = OutboxMessage.Create(Guid.NewGuid(), "reservation.hold_created", "Reservation", Guid.NewGuid(), "{}");
        var now = DateTime.UtcNow;

        // Act
        outbox.MarkAsFailed("Broker disconnected", now);

        // Assert
        outbox.Status.Should().Be(OutboxStatus.FAILED);
        outbox.ErrorMessage.Should().Be("Broker disconnected");
        outbox.ProcessedAt.Should().Be(now);
    }

    [Fact]
    public void IncrementRetry_ShouldIncrementRetryCountAndScheduleNextDelivery()
    {
        // Arrange
        var outbox = OutboxMessage.Create(Guid.NewGuid(), "reservation.hold_created", "Reservation", Guid.NewGuid(), "{}");

        // Act
        outbox.IncrementRetry("Transient timeout", nextDelaySeconds: 10);

        // Assert
        outbox.Status.Should().Be(OutboxStatus.PENDING);
        outbox.RetryCount.Should().Be(1);
        outbox.ErrorMessage.Should().Be("Transient timeout");
        outbox.DeliverAfter.Should().BeAfter(DateTime.UtcNow);
    }
}
