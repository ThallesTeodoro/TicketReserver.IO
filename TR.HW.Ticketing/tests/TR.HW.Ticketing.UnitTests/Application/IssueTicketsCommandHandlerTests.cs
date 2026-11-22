namespace TR.HW.Ticketing.UnitTests.Application;

using FluentAssertions;
using FluentValidation;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using TR.HW.Ticketing.Application.Features.IssueTickets;
using TR.HW.Ticketing.Application.Interfaces;
using TR.HW.Ticketing.Domain.Entities;
using TR.HW.Ticketing.Domain.Events;
using TR.HW.Ticketing.Domain.Exceptions;
using TR.HW.Ticketing.Domain.Services;

public sealed class IssueTicketsCommandHandlerTests
{
    private readonly Mock<ITicketingRepository> _mockRepository;
    private readonly Mock<IQrCodeGenerator> _mockQrGenerator;
    private readonly Mock<IKafkaTicketingProducer> _mockKafkaProducer;
    private readonly IValidator<IssueTicketsCommand> _validator;
    private readonly IssueTicketsCommandHandler _handler;

    public IssueTicketsCommandHandlerTests()
    {
        _mockRepository = new Mock<ITicketingRepository>();
        _mockQrGenerator = new Mock<IQrCodeGenerator>();
        _mockKafkaProducer = new Mock<IKafkaTicketingProducer>();
        _validator = new IssueTicketsCommandValidator();

        _mockQrGenerator
            .Setup(g => g.GenerateSignature(It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<DateTime>()))
            .Returns("mock_qr_hash_signature");

        _handler = new IssueTicketsCommandHandler(
            _mockRepository.Object,
            _mockQrGenerator.Object,
            _mockKafkaProducer.Object,
            _validator,
            NullLogger<IssueTicketsCommandHandler>.Instance);
    }

    [Fact]
    public async Task HandleAsync_ShouldIssueTicketsAndPublishEvent_WhenSeatsAreAvailable()
    {
        // Arrange
        var command = new IssueTicketsCommand(
            ReservationId: Guid.NewGuid(),
            PaymentId: Guid.NewGuid(),
            CustomerId: Guid.NewGuid(),
            EventId: Guid.NewGuid(),
            Items: new List<TicketIssuanceItemDto>
            {
                new(Guid.NewGuid(), Guid.NewGuid(), "Setor A - Assento 1", 150.00m),
                new(Guid.NewGuid(), Guid.NewGuid(), "Setor A - Assento 2", 150.00m)
            },
            MessageId: "msg-001",
            Traceparent: "00-4bf92f3577b34da6a3ce929d0e0e4736-00f067aa0ba902b7-01");

        _mockRepository
            .Setup(r => r.HasExistingTicketsForReservationAsync(command.ReservationId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        // Act
        var result = await _handler.HandleAsync(command);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Status.Should().Be("ISSUED");
        result.IssuedTickets.Should().HaveCount(2);
        result.FailureReason.Should().BeNull();

        _mockRepository.Verify(r => r.SaveTicketsAndAllocationsTransactionallyAsync(
            It.Is<IReadOnlyList<Ticket>>(t => t.Count == 2),
            It.Is<IReadOnlyList<SeatAllocation>>(a => a.Count == 2),
            "msg-001",
            "cg-ticketing-worker",
            It.IsAny<CancellationToken>()),
            Times.Once);

        _mockKafkaProducer.Verify(p => p.PublishTicketsIssuedAsync(
            command.ReservationId,
            command.PaymentId,
            command.CustomerId,
            command.EventId,
            It.Is<IReadOnlyList<TicketIssuedDomainEvent>>(e => e.Count == 2),
            command.Traceparent,
            It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task HandleAsync_ShouldRecordAndPublishFailedEvent_WhenSeatAlreadyAllocatedExceptionOccurs()
    {
        // Arrange
        var eventId = Guid.NewGuid();
        var collidedSeatId = Guid.NewGuid();
        var command = new IssueTicketsCommand(
            ReservationId: Guid.NewGuid(),
            PaymentId: Guid.NewGuid(),
            CustomerId: Guid.NewGuid(),
            EventId: eventId,
            Items: new List<TicketIssuanceItemDto>
            {
                new(Guid.NewGuid(), collidedSeatId, "Setor VIP - Assento 10", 300.00m)
            },
            MessageId: "msg-collision-001");

        _mockRepository
            .Setup(r => r.HasExistingTicketsForReservationAsync(command.ReservationId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        _mockRepository
            .Setup(r => r.SaveTicketsAndAllocationsTransactionallyAsync(
                It.IsAny<IReadOnlyList<Ticket>>(),
                It.IsAny<IReadOnlyList<SeatAllocation>>(),
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<CancellationToken>()))
            .ThrowsAsync(new SeatAlreadyAllocatedException(eventId, collidedSeatId));

        // Act
        var result = await _handler.HandleAsync(command);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Status.Should().Be("FAILED_SEAT_COLLISION");
        result.FailureReason.Should().Be("SEAT_ALREADY_ALLOCATED");
        result.IssuedTickets.Should().BeEmpty();

        _mockRepository.Verify(r => r.RecordTicketingFailedTransactionallyAsync(
            It.Is<TicketingFailedDomainEvent>(e =>
                e.ReservationId == command.ReservationId &&
                e.PaymentId == command.PaymentId &&
                e.FailureReason == "SEAT_ALREADY_ALLOCATED"),
            "msg-collision-001",
            "cg-ticketing-worker",
            It.IsAny<CancellationToken>()),
            Times.Once);

        _mockKafkaProducer.Verify(p => p.PublishTicketingFailedAsync(
            It.Is<TicketingFailedDomainEvent>(e =>
                e.ReservationId == command.ReservationId &&
                e.FailureReason == "SEAT_ALREADY_ALLOCATED"),
            It.IsAny<string?>(),
            It.IsAny<CancellationToken>()),
            Times.Once);

        _mockKafkaProducer.Verify(p => p.PublishTicketsIssuedAsync(
            It.IsAny<Guid>(),
            It.IsAny<Guid>(),
            It.IsAny<Guid>(),
            It.IsAny<Guid>(),
            It.IsAny<IReadOnlyList<TicketIssuedDomainEvent>>(),
            It.IsAny<string?>(),
            It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task HandleAsync_ShouldReturnAlreadyIssued_WhenReservationAlreadyHasTickets()
    {
        // Arrange
        var command = new IssueTicketsCommand(
            ReservationId: Guid.NewGuid(),
            PaymentId: Guid.NewGuid(),
            CustomerId: Guid.NewGuid(),
            EventId: Guid.NewGuid(),
            Items: new List<TicketIssuanceItemDto>
            {
                new(Guid.NewGuid(), Guid.NewGuid(), "Assento 5", 100.00m)
            });

        _mockRepository
            .Setup(r => r.HasExistingTicketsForReservationAsync(command.ReservationId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        // Act
        var result = await _handler.HandleAsync(command);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Status.Should().Be("ALREADY_ISSUED");

        _mockRepository.Verify(r => r.SaveTicketsAndAllocationsTransactionallyAsync(
            It.IsAny<IReadOnlyList<Ticket>>(),
            It.IsAny<IReadOnlyList<SeatAllocation>>(),
            It.IsAny<string>(),
            It.IsAny<string>(),
            It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task HandleAsync_ShouldThrowValidationException_WhenItemsAreEmpty()
    {
        // Arrange
        var command = new IssueTicketsCommand(
            ReservationId: Guid.NewGuid(),
            PaymentId: Guid.NewGuid(),
            CustomerId: Guid.NewGuid(),
            EventId: Guid.NewGuid(),
            Items: Array.Empty<TicketIssuanceItemDto>());

        // Act
        var act = async () => await _handler.HandleAsync(command);

        // Assert
        await act.Should().ThrowAsync<ValidationException>();
    }
}
