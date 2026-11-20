namespace TR.HW.Payment.UnitTests.Domain;

using FluentAssertions;
using TR.HW.Payment.Domain.Entities;
using TR.HW.Payment.Domain.Enums;
using TR.HW.Payment.Domain.Events;
using TR.HW.Payment.Domain.Exceptions;

public class PaymentDomainTests
{
    [Fact]
    public void CreatePending_WithValidInputs_ShouldInitializePaymentCorrectly()
    {
        var reservationId = Guid.NewGuid();
        var customerId = Guid.NewGuid();
        const decimal amount = 150.00m;

        var payment = Payment.CreatePending(
            reservationId,
            customerId,
            amount,
            PaymentMethod.CreditCard,
            "BRL",
            "STRIPE",
            "4242",
            "Visa");

        payment.Id.Should().NotBeEmpty();
        payment.ReservationId.Should().Be(reservationId);
        payment.CustomerId.Should().Be(customerId);
        payment.Amount.Should().Be(amount);
        payment.Currency.Should().Be("BRL");
        payment.PaymentMethod.Should().Be(PaymentMethod.CreditCard);
        payment.Status.Should().Be(PaymentStatus.Pending);
        payment.GatewayProvider.Should().Be("STRIPE");
        payment.CardLastFour.Should().Be("4242");
        payment.CardBrand.Should().Be("Visa");
        payment.Transactions.Should().BeEmpty();
        payment.DomainEvents.Should().BeEmpty();
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-10)]
    public void CreatePending_WithZeroOrNegativeAmount_ShouldThrowPaymentDomainException(decimal invalidAmount)
    {
        var act = () => Payment.CreatePending(Guid.NewGuid(), Guid.NewGuid(), invalidAmount);

        act.Should().Throw<PaymentDomainException>()
            .WithMessage("*maior que zero*");
    }

    [Fact]
    public void CreatePending_WithEmptyReservationId_ShouldThrowPaymentDomainException()
    {
        var act = () => Payment.CreatePending(Guid.Empty, Guid.NewGuid(), 100m);

        act.Should().Throw<PaymentDomainException>()
            .WithMessage("*ReservationId*");
    }

    [Fact]
    public void CreatePending_WithEmptyCustomerId_ShouldThrowPaymentDomainException()
    {
        var act = () => Payment.CreatePending(Guid.NewGuid(), Guid.Empty, 100m);

        act.Should().Throw<PaymentDomainException>()
            .WithMessage("*CustomerId*");
    }

    [Fact]
    public void Capture_WhenPending_ShouldTransitionToCapturedAndRaiseEvent()
    {
        var payment = Payment.CreatePending(Guid.NewGuid(), Guid.NewGuid(), 200m);

        payment.Capture("ch_stripe_123", "auth_456");

        payment.Status.Should().Be(PaymentStatus.Captured);
        payment.GatewayTransactionId.Should().Be("ch_stripe_123");
        payment.AuthorizationCode.Should().Be("auth_456");

        payment.Transactions.Should().HaveCount(1);
        var tx = payment.Transactions.First();
        tx.TransactionType.Should().Be(TransactionType.Capture);
        tx.Status.Should().Be(TransactionStatus.Success);
        tx.Amount.Should().Be(200m);
        tx.GatewayReference.Should().Be("ch_stripe_123");

        payment.DomainEvents.Should().HaveCount(1);
        var evt = payment.DomainEvents.First().Should().BeOfType<PaymentApprovedDomainEvent>().Subject;
        evt.PaymentId.Should().Be(payment.Id);
        evt.ReservationId.Should().Be(payment.ReservationId);
        evt.GatewayTransactionId.Should().Be("ch_stripe_123");
    }

    [Fact]
    public void Capture_WhenAlreadyCaptured_ShouldThrowPaymentDomainException()
    {
        var payment = Payment.CreatePending(Guid.NewGuid(), Guid.NewGuid(), 200m);
        payment.Capture("ch_stripe_123", "auth_456");

        var act = () => payment.Capture("ch_stripe_duplicate", "auth_duplicate");

        act.Should().Throw<PaymentDomainException>()
            .WithMessage("*Não é possível capturar pagamento com status atual 'Captured'*");
    }

    [Fact]
    public void Decline_WhenPending_ShouldTransitionToDeclinedAndRaiseEvent()
    {
        var payment = Payment.CreatePending(Guid.NewGuid(), Guid.NewGuid(), 300m);

        payment.Decline("Insufficient funds");

        payment.Status.Should().Be(PaymentStatus.Declined);
        payment.FailureReason.Should().Be("Insufficient funds");

        payment.Transactions.Should().HaveCount(1);
        var tx = payment.Transactions.First();
        tx.TransactionType.Should().Be(TransactionType.Authorize);
        tx.Status.Should().Be(TransactionStatus.Failed);
        tx.ErrorDetails.Should().Be("Insufficient funds");

        payment.DomainEvents.Should().HaveCount(1);
        var evt = payment.DomainEvents.First().Should().BeOfType<PaymentDeclinedDomainEvent>().Subject;
        evt.PaymentId.Should().Be(payment.Id);
        evt.FailureReason.Should().Be("Insufficient funds");
    }

    [Fact]
    public void Decline_WhenAlreadyDeclined_ShouldThrowPaymentDomainException()
    {
        var payment = Payment.CreatePending(Guid.NewGuid(), Guid.NewGuid(), 300m);
        payment.Decline("Card expired");

        var act = () => payment.Decline("Card reported stolen");

        act.Should().Throw<PaymentDomainException>()
            .WithMessage("*Não é possível recusar pagamento com status atual 'Declined'*");
    }

    [Fact]
    public void ClearDomainEvents_ShouldEmptyTheList()
    {
        var payment = Payment.CreatePending(Guid.NewGuid(), Guid.NewGuid(), 100m);
        payment.Capture("ch_123", "auth_123");

        payment.DomainEvents.Should().NotBeEmpty();
        payment.ClearDomainEvents();
        payment.DomainEvents.Should().BeEmpty();
    }
}
