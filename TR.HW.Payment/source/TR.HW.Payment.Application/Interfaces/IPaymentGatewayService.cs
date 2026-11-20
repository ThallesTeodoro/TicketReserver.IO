namespace TR.HW.Payment.Application.Interfaces;

public sealed record PaymentGatewayRequest(
    Guid ReservationId,
    Guid CustomerId,
    decimal Amount,
    string Currency,
    string PaymentMethod,
    string? CardLastFour = null,
    string? CardBrand = null);

public sealed record PaymentGatewayResult(
    bool IsSuccess,
    string? GatewayTransactionId,
    string? AuthorizationCode,
    string? FailureReason,
    string? RawResponse);

public interface IPaymentGatewayService
{
    Task<PaymentGatewayResult> ProcessPaymentAsync(PaymentGatewayRequest request, CancellationToken ct = default);
}
