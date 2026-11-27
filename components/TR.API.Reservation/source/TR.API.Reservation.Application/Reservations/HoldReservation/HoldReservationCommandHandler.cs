namespace TR.API.Reservation.Application.Reservations.HoldReservation;

using System.Text.Json;
using TR.API.Reservation.Application.Common.Messaging;
using TR.API.Reservation.Domain.Contracts.Caching;
using TR.API.Reservation.Domain.Contracts.Persistence;
using TR.API.Reservation.Domain.Entities;
using TR.API.Reservation.Domain.Exceptions;

public sealed class HoldReservationCommandHandler(
    IHoldCacheService holdCacheService,
    IReservationRepository reservationRepository,
    IUnitOfWork unitOfWork) : ICommandHandler<HoldReservationCommand, HoldReservationResponse>
{
    public async Task<HoldReservationResponse> HandleAsync(HoldReservationCommand command, CancellationToken cancellationToken = default)
    {
        var reservationId = Guid.NewGuid();
        var acquiredItems = new List<HoldItemDescriptor>();

        // 1. Tentar adquirir locks/cotas atômicas no Redis
        foreach (var item in command.Items)
        {
            var descriptor = new HoldItemDescriptor(item.TicketTierId, item.SeatId, item.Quantity);

            if (item.SeatId.HasValue)
            {
                var acquired = await holdCacheService.AcquireSeatHoldAsync(
                    command.EventId,
                    item.SeatId.Value,
                    reservationId,
                    TimeSpan.FromMinutes(10),
                    cancellationToken);

                if (!acquired)
                {
                    // Rollback Fast-Fail de locks já obtidos nesta requisição
                    await holdCacheService.ReleaseReservationHoldsAsync(command.EventId, reservationId, acquiredItems, cancellationToken);
                    throw new ConflictException($"O assento '{item.SeatLabel ?? item.SeatId.ToString()}' já está reservado por outro cliente.");
                }
            }
            else
            {
                var acquired = await holdCacheService.AcquireTierQuotaHoldAsync(
                    command.EventId,
                    item.TicketTierId,
                    item.Quantity,
                    reservationId,
                    TimeSpan.FromMinutes(10),
                    cancellationToken);

                if (!acquired)
                {
                    await holdCacheService.ReleaseReservationHoldsAsync(command.EventId, reservationId, acquiredItems, cancellationToken);
                    throw new ConflictException($"A quantidade solicitada para o lote '{item.TicketTierId}' não está disponível.");
                }
            }

            acquiredItems.Add(descriptor);
        }

        // 2. Persistir a reserva e outbox de forma atômica no banco relacional
        try
        {
            var reservation = Reservation.Create(command.CustomerId, command.EventId, TimeSpan.FromMinutes(10), command.Currency);

            foreach (var item in command.Items)
            {
                reservation.AddItem(item.TicketTierId, item.SeatId, item.SeatLabel, item.Quantity, item.UnitPrice);
            }

            var outboxPayload = JsonSerializer.Serialize(new
            {
                reservationId = reservation.Id,
                customerId = reservation.CustomerId,
                eventId = reservation.EventId,
                holdExpiresAt = reservation.HoldExpiresAt,
                totalAmount = reservation.TotalAmount,
                currency = reservation.Currency,
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
                eventType: "reservation.hold_created",
                aggregateType: "Reservation",
                aggregateId: reservation.Id,
                payload: outboxPayload,
                delaySeconds: 600);

            await reservationRepository.AddAsync(reservation, cancellationToken);
            await reservationRepository.AddOutboxMessageAsync(outbox, cancellationToken);
            await unitOfWork.CommitAsync(cancellationToken);

            return reservation.ToHoldResponse();
        }
        catch
        {
            // Compensação imediata no Redis em caso de falha de persistência no Postgres
            await holdCacheService.ReleaseReservationHoldsAsync(command.EventId, reservationId, acquiredItems, cancellationToken);
            throw;
        }
    }
}
