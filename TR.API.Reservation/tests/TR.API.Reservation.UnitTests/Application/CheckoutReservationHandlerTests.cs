namespace TR.API.Reservation.UnitTests.Application;

using FluentAssertions;
using FluentValidation.TestHelper;
using Moq;
using TR.API.Reservation.Application.Reservations.CheckoutReservation;
using TR.API.Reservation.Domain.Contracts.Caching;
using TR.API.Reservation.Domain.Contracts.Persistence;
using TR.API.Reservation.Domain.Entities;
using TR.API.Reservation.Domain.Enums;
using TR.API.Reservation.Domain.Exceptions;

public class CheckoutReservationHandlerTests
{
    private readonly Mock<IReservationRepository> _repoMock = new();
    private readonly Mock<IUnitOfWork> _uowMock = new();
    private readonly Mock<IHoldCacheService> _cacheMock = new();
    private readonly CheckoutReservationCommandHandler _sut;
    private readonly CheckoutReservationCommandValidator _validator = new();

    public CheckoutReservationHandlerTests()
    {
        _sut = new CheckoutReservationCommandHandler(_repoMock.Object, _uowMock.Object, _cacheMock.Object);
    }

    [Fact]
    public async Task HandleAsync_WhenValidAndWithinHold_ShouldTransitionToProcessingPaymentAndPublishOutbox()
    {
        // Arrange
        var customerId = Guid.NewGuid();
        var eventId = Guid.NewGuid();
        var reservation = Reservation.Create(customerId, eventId, TimeSpan.FromMinutes(10));
        reservation.AddItem(Guid.NewGuid(), null, null, 1, 100m);

        _repoMock.Setup(r => r.GetByIdAsync(reservation.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(reservation);

        _uowMock.Setup(u => u.CommitAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(1);

        var paymentDetails = new CheckoutPaymentDetailsDto("CREDIT_CARD", "tok_visa_123", 2, "12345678900");
        var command = new CheckoutReservationCommand(reservation.Id, customerId, paymentDetails);

        // Act
        var result = await _sut.HandleAsync(command);

        // Assert
        result.Should().NotBeNull();
        result.Status.Should().Be(ReservationStatus.PROCESSING_PAYMENT.ToString());
        reservation.Status.Should().Be(ReservationStatus.PROCESSING_PAYMENT);
        reservation.Version.Should().Be(2);

        _repoMock.Verify(r => r.AddOutboxMessageAsync(
            It.Is<OutboxMessage>(m => m.EventType == "reservation.checkout_req" && m.DelaySeconds == 0),
            It.IsAny<CancellationToken>()), Times.Once);
        _uowMock.Verify(u => u.CommitAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task HandleAsync_WhenReservationNotFound_ShouldThrowNotFoundException()
    {
        // Arrange
        var resId = Guid.NewGuid();
        _repoMock.Setup(r => r.GetByIdAsync(resId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((Reservation?)null);

        var command = new CheckoutReservationCommand(resId, Guid.NewGuid(), new CheckoutPaymentDetailsDto("PIX"));

        // Act
        var act = async () => await _sut.HandleAsync(command);

        // Assert
        await act.Should().ThrowAsync<NotFoundException>().WithMessage("*não foi encontrada*");
    }

    [Fact]
    public async Task HandleAsync_WhenCustomerMismatch_ShouldThrowDomainException()
    {
        // Arrange
        var customerId = Guid.NewGuid();
        var reservation = Reservation.Create(customerId, Guid.NewGuid(), TimeSpan.FromMinutes(10));
        reservation.AddItem(Guid.NewGuid(), null, null, 1, 100m);

        _repoMock.Setup(r => r.GetByIdAsync(reservation.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(reservation);

        var differentCustomer = Guid.NewGuid();
        var command = new CheckoutReservationCommand(reservation.Id, differentCustomer, new CheckoutPaymentDetailsDto("PIX"));

        // Act
        var act = async () => await _sut.HandleAsync(command);

        // Assert
        await act.Should().ThrowAsync<DomainException>().WithMessage("*pertence a outro cliente*");
    }

    [Fact]
    public async Task HandleAsync_WhenHoldExpired_ShouldExpireReservationReleaseRedisAndThrowConflictException()
    {
        // Arrange
        var customerId = Guid.NewGuid();
        var reservation = Reservation.Create(customerId, Guid.NewGuid(), TimeSpan.FromSeconds(1));
        reservation.AddItem(Guid.NewGuid(), null, null, 1, 100m);

        // Simula passagem de tempo
        await Task.Delay(1050);

        _repoMock.Setup(r => r.GetByIdAsync(reservation.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(reservation);

        var command = new CheckoutReservationCommand(reservation.Id, customerId, new CheckoutPaymentDetailsDto("PIX"));

        // Act
        var act = async () => await _sut.HandleAsync(command);

        // Assert
        await act.Should().ThrowAsync<ConflictException>().WithMessage("*expirou*");
        reservation.Status.Should().Be(ReservationStatus.EXPIRED);
        _cacheMock.Verify(c => c.ReleaseReservationHoldsAsync(reservation.EventId, reservation.Id, It.IsAny<IEnumerable<HoldItemDescriptor>>(), It.IsAny<CancellationToken>()), Times.Once);
        _uowMock.Verify(u => u.CommitAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public void Validator_WhenPaymentMethodInvalid_ShouldHaveValidationError()
    {
        // Arrange
        var command = new CheckoutReservationCommand(
            Guid.NewGuid(),
            Guid.NewGuid(),
            new CheckoutPaymentDetailsDto("BITCOIN"));

        // Act
        var result = _validator.TestValidate(command);

        // Assert
        result.ShouldHaveValidationErrorFor(x => x.PaymentDetails.PaymentMethod);
    }
}
