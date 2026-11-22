namespace TR.HW.Refund.Application.Features.ProcessRefund;

using FluentValidation;
using Microsoft.Extensions.Logging;
using TR.HW.Refund.Application.Common.Messaging;
using TR.HW.Refund.Application.Interfaces;
using TR.HW.Refund.Domain.Entities;
using TR.HW.Refund.Domain.Events;

public sealed class ProcessRefundCommandHandler(
    IRefundRepository refundRepository,
    IRefundGatewayService refundGatewayService,
    IKafkaRefundProducer kafkaRefundProducer,
    IValidator<ProcessRefundCommand> validator,
    ILogger<ProcessRefundCommandHandler> logger) : ICommandHandler<ProcessRefundCommand, ProcessRefundResult>
{
    private const string ConsumerGroup = "cg-payment-refund";

    public async Task<ProcessRefundResult> HandleAsync(ProcessRefundCommand command, CancellationToken ct = default)
    {
        var validationResult = await validator.ValidateAsync(command, ct);
        if (!validationResult.IsValid)
        {
            var errors = string.Join(", ", validationResult.Errors.Select(e => e.ErrorMessage));
            logger.LogWarning("Validation failed for ProcessRefundCommand: {Errors}", errors);
            throw new ValidationException(validationResult.Errors);
        }

        var messageId = command.MessageId ?? command.ReservationId.ToString();

        // 1. Lookup payment by reservation_id
        var payment = await refundRepository.GetPaymentByReservationIdAsync(command.ReservationId, ct);
        if (payment is null)
        {
            logger.LogWarning("No payment found for Reservation {ReservationId}. Skipping refund and recording inbox.", command.ReservationId);
            await refundRepository.RecordProcessedMessageAsync(messageId, ConsumerGroup, "ticketing.v1.failed", ct);
            return new ProcessRefundResult(false, null, "PAYMENT_NOT_FOUND", null, "PAYMENT_NOT_FOUND");
        }

        // 2. Idempotency check: already refunded
        if (payment.IsAlreadyRefunded)
        {
            logger.LogInformation("Payment {PaymentId} for Reservation {ReservationId} is already refunded. Skipping duplicate refund.", payment.Id, command.ReservationId);
            await refundRepository.RecordProcessedMessageAsync(messageId, ConsumerGroup, "ticketing.v1.failed", ct);
            return new ProcessRefundResult(true, null, "ALREADY_REFUNDED", null);
        }

        // 3. Check if payment is in CAPTURED status
        if (!payment.CanBeRefunded)
        {
            logger.LogWarning("Payment {PaymentId} for Reservation {ReservationId} is in status {Status} and cannot be refunded.", payment.Id, command.ReservationId, payment.Status);
            await refundRepository.RecordProcessedMessageAsync(messageId, ConsumerGroup, "ticketing.v1.failed", ct);
            return new ProcessRefundResult(false, null, "NOT_ELIGIBLE_FOR_REFUND", null, $"Payment status is {payment.Status}");
        }

        // 4. Create pending Refund aggregate
        var refund = Refund.CreatePending(
            paymentId: payment.Id,
            reservationId: payment.ReservationId,
            customerId: payment.CustomerId,
            amount: payment.Amount,
            currency: payment.Currency,
            reason: command.Reason);

        // 5. Invoke resilient payment gateway for refund
        var gatewayRequest = payment.ToGatewayRequest(command.Reason);
        var gatewayResult = await refundGatewayService.ProcessRefundAsync(gatewayRequest, ct);

        if (gatewayResult.IsSuccess)
        {
            refund.Complete(
                gatewayRefundId: gatewayResult.GatewayRefundId!,
                requestPayload: null,
                responsePayload: gatewayResult.RawResponse);
        }
        else
        {
            refund.Fail(
                errorDetails: gatewayResult.FailureReason ?? "Gateway refund failed",
                requestPayload: null,
                responsePayload: gatewayResult.RawResponse);
        }

        // 6. Atomically save payment status update, refund record, transaction ledger, outbox, and inbox
        await refundRepository.SaveRefundTransactionallyAsync(refund, messageId, ConsumerGroup, ct);

        // 7. Publish domain events (payment.v1.refunded) to Kafka
        foreach (var evt in refund.DomainEvents.OfType<PaymentRefundedDomainEvent>())
        {
            await kafkaRefundProducer.PublishPaymentRefundedAsync(evt, command.Traceparent, ct);
        }

        refund.ClearDomainEvents();

        return refund.ToProcessRefundResult();
    }
}
