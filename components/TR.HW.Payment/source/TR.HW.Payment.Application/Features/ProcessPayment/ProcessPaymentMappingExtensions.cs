namespace TR.HW.Payment.Application.Features.ProcessPayment;

using TR.HW.Payment.Application.Interfaces;
using TR.HW.Payment.Domain.Entities;
using TR.HW.Payment.Domain.Enums;

public static class ProcessPaymentMappingExtensions
{
    public static PaymentGatewayRequest ToGatewayRequest(this ProcessPaymentCommand command) =>
        new(
            command.ReservationId,
            command.CustomerId,
            command.TotalAmount,
            command.Currency,
            command.PaymentMethod,
            command.CardLastFour,
            command.CardBrand);

    public static PaymentMethod ParsePaymentMethod(string? method) => method?.ToUpperInvariant() switch
    {
        "DEBIT_CARD" or "DEBITCARD" => PaymentMethod.DebitCard,
        "PIX" => PaymentMethod.Pix,
        _ => PaymentMethod.CreditCard
    };

    public static ProcessPaymentResult ToProcessPaymentResult(this Payment payment) =>
        new(
            payment.Status == PaymentStatus.Captured,
            payment.Id,
            payment.Status.ToString(),
            payment.GatewayTransactionId,
            payment.FailureReason);
}
