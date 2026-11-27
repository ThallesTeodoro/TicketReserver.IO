namespace TR.HW.Refund.Application.Interfaces;

public sealed record RefundGatewayRequest(
    Guid PaymentId,
    Guid ReservationId,
    string GatewayTransactionId,
    decimal Amount,
    string Currency,
    string Reason);

public sealed record RefundGatewayResult(
    bool IsSuccess,
    string? GatewayRefundId,
    string? FailureReason,
    string? RawResponse);

public interface IRefundGatewayService
{
    Task<RefundGatewayResult> ProcessRefundAsync(RefundGatewayRequest request, CancellationToken ct = default);
}
