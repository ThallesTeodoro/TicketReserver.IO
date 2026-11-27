namespace TR.HW.Refund.Application.Features.ProcessRefund;

using TR.HW.Refund.Application.Interfaces;
using TR.HW.Refund.Domain.Entities;
using TR.HW.Refund.Domain.Enums;

public static class ProcessRefundMappingExtensions
{
    public static RefundGatewayRequest ToGatewayRequest(this PaymentSnapshot payment, string reason)
    {
        return new RefundGatewayRequest(
            PaymentId: payment.Id,
            ReservationId: payment.ReservationId,
            GatewayTransactionId: payment.GatewayTransactionId ?? $"ch_fallback_{payment.Id:N}",
            Amount: payment.Amount,
            Currency: payment.Currency,
            Reason: reason);
    }

    public static ProcessRefundResult ToProcessRefundResult(this Refund refund)
    {
        var statusStr = refund.Status switch
        {
            RefundStatus.Completed => "REFUNDED",
            RefundStatus.Failed => "FAILED",
            _ => "PENDING"
        };

        return new ProcessRefundResult(
            IsRefunded: refund.Status == RefundStatus.Completed,
            RefundId: refund.Id,
            Status: statusStr,
            GatewayRefundId: refund.GatewayRefundId,
            FailureReason: refund.Transactions.LastOrDefault()?.ErrorDetails);
    }
}
