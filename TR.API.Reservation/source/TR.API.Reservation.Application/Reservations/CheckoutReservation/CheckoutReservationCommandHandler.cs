namespace TR.API.Reservation.Application.Reservations.CheckoutReservation;

using System.Text.Json;
using TR.API.Reservation.Application.Common.Messaging;
using TR.API.Reservation.Domain.Contracts.Caching;
using TR.API.Reservation.Domain.Contracts.Persistence;
using TR.API.Reservation.Domain.Entities;
using TR.API.Reservation.Domain.Enums;
using TR.API.Reservation.Domain.Exceptions;

public sealed class CheckoutReservationCommandHandler(
    IReservationRepository reservationRepository,
    IUnitOfWork unitOfWork,
    IHoldCacheService holdCacheService) : ICommandHandler<CheckoutReservationCommand, CheckoutReservationResponse>
{
    public async Task<CheckoutReservationResponse> HandleAsync(CheckoutReservationCommand command, CancellationToken cancellationToken = default)
    {
        var reservation = await reservationRepository.GetByIdAsync(command.ReservationId, cancellationToken)
            ?? throw new NotFoundException($"Reserva com Id '{command.ReservationId}' não foi encontrada.");

        if (reservation.CustomerId != command.CustomerId)
            throw new DomainException("Esta reserva pertence a outro cliente.");

        if (reservation.Status != ReservationStatus.AWAITING_PAYMENT)
            throw new ConflictException($"Não é possível iniciar checkout para uma reserva com status '{reservation.Status}'.");

        var now = DateTime.UtcNow;

        // Se o hold expirou, expira a reserva, libera assentos no Redis e rejeita com 409
        if (now >= reservation.HoldExpiresAt)
        {
            reservation.Expire(now);

            var items = reservation.Items.Select(i => new HoldItemDescriptor(i.TicketTierId, i.SeatId, i.Quantity));
            await holdCacheService.ReleaseReservationHoldsAsync(reservation.EventId, reservation.Id, items, cancellationToken);
            await unitOfWork.CommitAsync(cancellationToken);

            throw new ConflictException("O tempo limite de 10 minutos para reserva deste pedido expirou.");
        }

        // Transição de estado: AWAITING_PAYMENT -> PROCESSING_PAYMENT
        reservation.StartPayment(now);

        var outboxPayload = JsonSerializer.Serialize(new
        {
            reservationId = reservation.Id,
            customerId = reservation.CustomerId,
            eventId = reservation.EventId,
            totalAmount = reservation.TotalAmount,
            currency = reservation.Currency,
            paymentMethod = command.PaymentDetails.PaymentMethod,
            cardToken = command.PaymentDetails.CardToken,
            installments = command.PaymentDetails.Installments,
            customerDocument = command.PaymentDetails.CustomerDocument,
            paymentAttemptedAt = reservation.PaymentAttemptedAt,
            items = reservation.Items.Select(i => new
            {
                ticketTierId = i.TicketTierId,
                seatId = i.SeatId,
                seatLabel = i.SeatLabel,
                quantity = i.Quantity,
                unitPrice = i.UnitPrice,
                subtotal = i.Subtotal
            })
        });

        var outbox = OutboxMessage.Create(
            correlationId: reservation.Id,
            eventType: "reservation.checkout_req",
            aggregateType: "Reservation",
            aggregateId: reservation.Id,
            payload: outboxPayload,
            delaySeconds: 0);

        await reservationRepository.AddOutboxMessageAsync(outbox, cancellationToken);
        await unitOfWork.CommitAsync(cancellationToken);

        return new CheckoutReservationResponse(
            reservation.Id,
            reservation.Status.ToString(),
            reservation.TotalAmount,
            reservation.Currency,
            reservation.PaymentAttemptedAt!.Value);
    }
}
