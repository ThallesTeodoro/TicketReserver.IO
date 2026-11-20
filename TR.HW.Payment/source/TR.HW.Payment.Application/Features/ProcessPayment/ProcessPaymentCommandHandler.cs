namespace TR.HW.Payment.Application.Features.ProcessPayment;

using FluentValidation;
using Microsoft.Extensions.Logging;
using TR.HW.Payment.Application.Common.Messaging;
using TR.HW.Payment.Application.Interfaces;
using TR.HW.Payment.Domain.Entities;
using TR.HW.Payment.Domain.Events;

public sealed class ProcessPaymentCommandHandler(
    IPaymentRepository paymentRepository,
    IPaymentGatewayService paymentGatewayService,
    IKafkaPaymentProducer kafkaPaymentProducer,
    IValidator<ProcessPaymentCommand> validator,
    ILogger<ProcessPaymentCommandHandler> logger) : ICommandHandler<ProcessPaymentCommand, ProcessPaymentResult>
{
    private const string ConsumerGroup = "cg-payment-processing";

    public async Task<ProcessPaymentResult> HandleAsync(ProcessPaymentCommand command, CancellationToken ct = default)
    {
        var validationResult = await validator.ValidateAsync(command, ct);
        if (!validationResult.IsValid)
        {
            var errors = string.Join(", ", validationResult.Errors.Select(e => e.ErrorMessage));
            logger.LogWarning("Falha de validação no comando de pagamento: {Errors}", errors);
            throw new ValidationException(validationResult.Errors);
        }

        var messageId = command.MessageId ?? command.ReservationId.ToString();

        // 1. Idempotência por reserva: Impede que a mesma reserva seja processada novamente
        if (await paymentRepository.HasExistingPaymentForReservationAsync(command.ReservationId, ct))
        {
            logger.LogInformation("Reserva {ReservationId} já possui pagamento registrado. Ignorando solicitação duplicada.", command.ReservationId);
            await paymentRepository.RecordProcessedMessageAsync(messageId, ConsumerGroup, "reservation.v1.checkout_req", ct);
            return new ProcessPaymentResult(true, Guid.Empty, "ALREADY_PROCESSED", null, null);
        }

        var paymentMethod = ProcessPaymentMappingExtensions.ParsePaymentMethod(command.PaymentMethod);

        // 2. Instancia o agregado em estado Pending
        var payment = Payment.CreatePending(
            command.ReservationId,
            command.CustomerId,
            command.TotalAmount,
            paymentMethod,
            command.Currency,
            "STRIPE",
            command.CardLastFour,
            command.CardBrand);

        // 3. Invoca o gateway de pagamento resiliente (Polly)
        var gatewayRequest = command.ToGatewayRequest();
        var gatewayResult = await paymentGatewayService.ProcessPaymentAsync(gatewayRequest, ct);

        // 4. Atualiza o agregado com o resultado
        if (gatewayResult.IsSuccess)
        {
            payment.Capture(
                gatewayResult.GatewayTransactionId!,
                gatewayResult.AuthorizationCode!,
                requestPayload: null,
                responsePayload: gatewayResult.RawResponse);
        }
        else
        {
            payment.Decline(
                gatewayResult.FailureReason ?? "Recusado pelo gateway",
                requestPayload: null,
                responsePayload: gatewayResult.RawResponse);
        }

        // 5. Persistência atômica no PostgreSQL (Payment + Transaction Ledger + Outbox + Inbox)
        await paymentRepository.SavePaymentTransactionallyAsync(payment, messageId, ConsumerGroup, ct);

        // 6. Publicação de eventos de domínio no Apache Kafka
        foreach (var domainEvent in payment.DomainEvents)
        {
            switch (domainEvent)
            {
                case PaymentApprovedDomainEvent approved:
                    await kafkaPaymentProducer.PublishPaymentApprovedAsync(approved, command.Traceparent, ct);
                    break;
                case PaymentDeclinedDomainEvent declined:
                    await kafkaPaymentProducer.PublishPaymentDeclinedAsync(declined, command.Traceparent, ct);
                    break;
            }
        }

        payment.ClearDomainEvents();

        return payment.ToProcessPaymentResult();
    }
}
