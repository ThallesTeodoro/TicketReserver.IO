namespace TR.HW.Ticketing.UnitTests.Domain;

using FluentAssertions;
using Moq;
using TR.HW.Ticketing.Domain.Entities;
using TR.HW.Ticketing.Domain.Enums;
using TR.HW.Ticketing.Domain.Events;
using TR.HW.Ticketing.Domain.Exceptions;
using TR.HW.Ticketing.Domain.Services;

public sealed class TicketDomainTests
{
    private readonly Mock<IQrCodeGenerator> _mockQrCodeGenerator;

    public TicketDomainTests()
    {
        _mockQrCodeGenerator = new Mock<IQrCodeGenerator>();
        _mockQrCodeGenerator
            .Setup(g => g.GenerateSignature(It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<DateTime>()))
            .Returns("hmac_sha256_valid_signature_hash");
    }

    [Fact]
    public void Issue_ShouldCreateActiveTicketAndRaiseDomainEvent_WhenParametersAreValid()
    {
        // Arrange
        var reservationId = Guid.NewGuid();
        var paymentId = Guid.NewGuid();
        var customerId = Guid.NewGuid();
        var eventId = Guid.NewGuid();
        var tierId = Guid.NewGuid();
        var seatId = Guid.NewGuid();

        // Act
        var ticket = Ticket.Issue(
            reservationId,
            paymentId,
            customerId,
            eventId,
            tierId,
            180.00m,
            _mockQrCodeGenerator.Object,
            seatId,
            "Setor Oeste, Assento 12");

        // Assert
        ticket.Id.Should().NotBeEmpty();
        ticket.TicketCode.Should().StartWith($"TKT-{DateTime.UtcNow.Year}-");
        ticket.QrCodeHash.Should().Be("hmac_sha256_valid_signature_hash");
        ticket.ReservationId.Should().Be(reservationId);
        ticket.PaymentId.Should().Be(paymentId);
        ticket.CustomerId.Should().Be(customerId);
        ticket.EventId.Should().Be(eventId);
        ticket.TicketTierId.Should().Be(tierId);
        ticket.SeatId.Should().Be(seatId);
        ticket.SeatLabel.Should().Be("Setor Oeste, Assento 12");
        ticket.Status.Should().Be(TicketStatus.Active);
        ticket.PricePaid.Should().Be(180.00m);
        ticket.Version.Should().Be(1);
        ticket.DomainEvents.Should().ContainSingle()
            .Which.Should().BeOfType<TicketIssuedDomainEvent>();
    }

    [Fact]
    public void Issue_ShouldThrowDomainException_WhenPricePaidIsNegative()
    {
        // Arrange
        var act = () => Ticket.Issue(
            Guid.NewGuid(),
            Guid.NewGuid(),
            Guid.NewGuid(),
            Guid.NewGuid(),
            Guid.NewGuid(),
            -10.00m,
            _mockQrCodeGenerator.Object);

        // Act & Assert
        act.Should().Throw<DomainException>()
            .WithMessage("*PricePaid cannot be negative*");
    }

    [Fact]
    public void Issue_ShouldThrowDomainException_WhenReservationIdIsEmpty()
    {
        // Arrange
        var act = () => Ticket.Issue(
            Guid.Empty,
            Guid.NewGuid(),
            Guid.NewGuid(),
            Guid.NewGuid(),
            Guid.NewGuid(),
            100.00m,
            _mockQrCodeGenerator.Object);

        // Act & Assert
        act.Should().Throw<DomainException>()
            .WithMessage("*ReservationId cannot be empty*");
    }

    [Fact]
    public void CheckIn_ShouldTransitionToUsedAndRecordCheckin_WhenTicketIsActive()
    {
        // Arrange
        var ticket = Ticket.Issue(
            Guid.NewGuid(),
            Guid.NewGuid(),
            Guid.NewGuid(),
            Guid.NewGuid(),
            Guid.NewGuid(),
            150.00m,
            _mockQrCodeGenerator.Object);

        // Act
        ticket.CheckIn("Gate A", "Scanner-01");

        // Assert
        ticket.Status.Should().Be(TicketStatus.Used);
        ticket.CheckedInAt.Should().NotBeNull();
        ticket.Version.Should().Be(2);
        ticket.Checkins.Should().ContainSingle(c =>
            c.GateName == "Gate A" &&
            c.DeviceId == "Scanner-01" &&
            c.Status == CheckinStatus.Approved);
    }

    [Fact]
    public void CheckIn_ShouldRecordAlreadyUsedAndThrow_WhenTicketAlreadyUsed()
    {
        // Arrange
        var ticket = Ticket.Issue(
            Guid.NewGuid(),
            Guid.NewGuid(),
            Guid.NewGuid(),
            Guid.NewGuid(),
            Guid.NewGuid(),
            150.00m,
            _mockQrCodeGenerator.Object);

        ticket.CheckIn("Gate A", "Scanner-01");

        // Act
        var act = () => ticket.CheckIn("Gate B", "Scanner-02");

        // Assert
        act.Should().Throw<DomainException>()
            .WithMessage("*already been used*");
        ticket.Checkins.Should().HaveCount(2);
        ticket.Checkins.Last().Status.Should().Be(CheckinStatus.AlreadyUsed);
    }

    [Fact]
    public void Revoke_ShouldTransitionToRevoked_WhenTicketIsActive()
    {
        // Arrange
        var ticket = Ticket.Issue(
            Guid.NewGuid(),
            Guid.NewGuid(),
            Guid.NewGuid(),
            Guid.NewGuid(),
            Guid.NewGuid(),
            150.00m,
            _mockQrCodeGenerator.Object);

        // Act
        ticket.Revoke("AUTO_REFUND_SEAT_COLLISION");

        // Assert
        ticket.Status.Should().Be(TicketStatus.Revoked);
        ticket.RevokedAt.Should().NotBeNull();
        ticket.RevocationReason.Should().Be("AUTO_REFUND_SEAT_COLLISION");
        ticket.Version.Should().Be(2);
    }

    [Fact]
    public void Revoke_ShouldThrowDomainException_WhenTicketIsAlreadyUsed()
    {
        // Arrange
        var ticket = Ticket.Issue(
            Guid.NewGuid(),
            Guid.NewGuid(),
            Guid.NewGuid(),
            Guid.NewGuid(),
            Guid.NewGuid(),
            150.00m,
            _mockQrCodeGenerator.Object);

        ticket.CheckIn("Gate A", "Scanner-01");

        // Act
        var act = () => ticket.Revoke("FRAUD");

        // Assert
        act.Should().Throw<DomainException>()
            .WithMessage("*Cannot revoke an already used ticket*");
    }

    [Fact]
    public void CreateSeatAllocation_ShouldUseNumberedSeatType_WhenSeatIdIsPresent()
    {
        // Arrange
        var seatId = Guid.NewGuid();
        var ticket = Ticket.Issue(
            Guid.NewGuid(),
            Guid.NewGuid(),
            Guid.NewGuid(),
            Guid.NewGuid(),
            Guid.NewGuid(),
            150.00m,
            _mockQrCodeGenerator.Object,
            seatId,
            "A10");

        // Act
        var allocation = ticket.CreateSeatAllocation();

        // Assert
        allocation.SeatId.Should().Be(seatId);
        allocation.TicketId.Should().Be(ticket.Id);
        allocation.AllocationType.Should().Be(AllocationType.NumberedSeat);
    }

    [Fact]
    public void CreateSeatAllocation_ShouldSynthesizeSeatIdAndUseGeneralAdmission_WhenSeatIdIsNull()
    {
        // Arrange
        var ticket = Ticket.Issue(
            Guid.NewGuid(),
            Guid.NewGuid(),
            Guid.NewGuid(),
            Guid.NewGuid(),
            Guid.NewGuid(),
            150.00m,
            _mockQrCodeGenerator.Object,
            seatId: null,
            seatLabel: "Pista Geral");

        // Act
        var allocation = ticket.CreateSeatAllocation();

        // Assert
        allocation.SeatId.Should().Be(ticket.Id);
        allocation.AllocationType.Should().Be(AllocationType.GeneralAdmission);
    }
}
