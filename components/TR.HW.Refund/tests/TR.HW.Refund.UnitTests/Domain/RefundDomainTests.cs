namespace TR.HW.Refund.UnitTests.Domain;

using FluentAssertions;
using TR.HW.Refund.Domain.Entities;
using TR.HW.Refund.Domain.Enums;
using TR.HW.Refund.Domain.Events;
using TR.HW.Refund.Domain.Exceptions;

public sealed class RefundDomainTests
{
    [Fact]
    public void CreatePending_ShouldInitializeValidPendingRefund_WhenInputsAreValid()
    {
        // Arrange
        var paymentId = Guid.NewGuid();
        var reservationId = Guid.NewGuid();
        var customerId = Guid.NewGuid();

        // Act
        var refund = Refund.CreatePending(
            paymentId,
            reservationId,
            customerId,
            250.00m,
            "BRL",
            "SEAT_ALREADY_ALLOCATED");

        // Assert
        refund.Id.Should().NotBeEmpty();
        refund.PaymentId.Should().Be(paymentId);
        refund.ReservationId.Should().Be(reservationId);
        refund.CustomerId.Should().Be(customerId);
        refund.Amount.Should().Be(250.00m);
        refund.Currency.Should().Be("BRL");
        refund.Reason.Should().Be("SEAT_ALREADY_ALLOCATED");
        refund.Status.Should().Be(RefundStatus.Pending);
        refund.Transactions.Should().BeEmpty();
        refund.DomainEvents.Should().BeEmpty();
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-50.00)]
    public void CreatePending_ShouldThrowDomainException_WhenAmountIsZeroOrNegative(decimal invalidAmount)
    {
        // Arrange
        var act = () => Refund.CreatePending(
            Guid.NewGuid(),
            Guid.NewGuid(),
            Guid.NewGuid(),
            invalidAmount,
            "BRL",
            "SEAT_ALREADY_ALLOCATED");

        // Act & Assert
        act.Should().Throw<DomainException>()
            .WithMessage("*amount must be greater than zero*");
    }

    [Fact]
    public void CreatePending_ShouldThrowDomainException_WhenReasonIsEmpty()
    {
        // Arrange
        var act = () => Refund.CreatePending(
            Guid.NewGuid(),
            Guid.NewGuid(),
            Guid.NewGuid(),
            100.00m,
            "BRL",
            "   ");

        // Act & Assert
        act.Should().Throw<DomainException>()
            .WithMessage("*reason cannot be empty*");
    }

    [Fact]
    public void Complete_ShouldTransitionToCompletedAndRaiseDomainEvent_WhenRefundIsPending()
    {
        // Arrange
        var refund = Refund.CreatePending(
            Guid.NewGuid(),
            Guid.NewGuid(),
            Guid.NewGuid(),
            180.00m,
            "BRL",
            "SEAT_ALREADY_ALLOCATED");

        // Act
        refund.Complete("re_test_12345", "{\"req\":1}", "{\"status\":\"succeeded\"}");

        // Assert
        refund.Status.Should().Be(RefundStatus.Completed);
        refund.GatewayRefundId.Should().Be("re_test_12345");
        refund.GatewayResponse.Should().Contain("succeeded");
        refund.Transactions.Should().ContainSingle(tx =>
            tx.TransactionType == TransactionType.Refund &&
            tx.Status == TransactionStatus.Success &&
            tx.GatewayReference == "re_test_12345");
        refund.DomainEvents.Should().ContainSingle()
            .Which.Should().BeOfType<PaymentRefundedDomainEvent>();
    }

    [Fact]
    public void Complete_ShouldThrowDomainException_WhenRefundIsAlreadyCompleted()
    {
        // Arrange
        var refund = Refund.CreatePending(
            Guid.NewGuid(),
            Guid.NewGuid(),
            Guid.NewGuid(),
            180.00m,
            "BRL",
            "SEAT_ALREADY_ALLOCATED");

        refund.Complete("re_test_12345");

        // Act
        var act = () => refund.Complete("re_test_67890");

        // Assert
        act.Should().Throw<DomainException>()
            .WithMessage("*Cannot complete a refund that is in status*");
    }

    [Fact]
    public void Fail_ShouldTransitionToFailedAndRecordFailedTransaction_WhenRefundIsPending()
    {
        // Arrange
        var refund = Refund.CreatePending(
            Guid.NewGuid(),
            Guid.NewGuid(),
            Guid.NewGuid(),
            180.00m,
            "BRL",
            "SEAT_ALREADY_ALLOCATED");

        // Act
        refund.Fail("GATEWAY_REJECTED_REFUND", null, "{\"error\":\"charge_already_refunded\"}");

        // Assert
        refund.Status.Should().Be(RefundStatus.Failed);
        refund.Transactions.Should().ContainSingle(tx =>
            tx.Status == TransactionStatus.Failed &&
            tx.ErrorDetails == "GATEWAY_REJECTED_REFUND");
        refund.DomainEvents.Should().BeEmpty();
    }

    [Fact]
    public void PaymentSnapshot_ShouldEvaluateCanBeRefundedAndIsAlreadyRefundedCorrectly()
    {
        // Arrange
        var capturedPayment = new PaymentSnapshot(
            Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), 150.00m, "BRL",
            PaymentStatus.Captured, "STRIPE", "ch_123", 1);

        var refundedPayment = capturedPayment with { Status = PaymentStatus.Refunded };

        // Act & Assert
        capturedPayment.CanBeRefunded.Should().BeTrue();
        capturedPayment.IsAlreadyRefunded.Should().BeFalse();

        refundedPayment.CanBeRefunded.Should().BeFalse();
        refundedPayment.IsAlreadyRefunded.Should().BeTrue();
    }
}
