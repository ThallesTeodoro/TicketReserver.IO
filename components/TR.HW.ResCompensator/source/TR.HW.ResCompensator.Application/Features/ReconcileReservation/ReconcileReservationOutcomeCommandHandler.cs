namespace TR.HW.ResCompensator.Application.Features.ReconcileReservation;

using FluentValidation;
using Microsoft.Extensions.Logging;
using TR.HW.ResCompensator.Application.Common.Messaging;
using TR.HW.ResCompensator.Application.Interfaces;
using TR.HW.ResCompensator.Domain.Enums;

public sealed class ReconcileReservationOutcomeCommandHandler(
    IReservationCompensationRepository repository,
    IRedisSeatLockReleaser seatLockReleaser,
    IValidator<ReconcileReservationOutcomeCommand> validator,
    ILogger<ReconcileReservationOutcomeCommandHandler> logger)
    : ICommandHandler<ReconcileReservationOutcomeCommand, ReconcileReservationOutcomeResult>
{
    private const string ConsumerGroup = "cg-reservation-compensator";

    public async Task<ReconcileReservationOutcomeResult> HandleAsync(
        ReconcileReservationOutcomeCommand command,
        CancellationToken ct = default)
    {
        var validationResult = await validator.ValidateAsync(command, ct);
        if (!validationResult.IsValid)
        {
            var errors = string.Join(", ", validationResult.Errors.Select(e => e.ErrorMessage));
            logger.LogWarning("Validation failed for ReconcileReservationOutcomeCommand: {Errors}", errors);
            throw new ValidationException(validationResult.Errors);
        }

        var messageId = command.MessageId ?? command.ReservationId.ToString();

        var reservation = await repository.GetReservationByIdAsync(command.ReservationId, ct);
        if (reservation is null)
        {
            logger.LogWarning(
                "Reservation {ReservationId} not found in reservation_db. Recording message in inbox and skipping.",
                command.ReservationId);

            await repository.RecordProcessedMessageAsync(messageId, ConsumerGroup, command.EventType, ct);
            return new ReconcileReservationOutcomeResult(
                IsReconciled: false,
                ReservationId: command.ReservationId,
                FinalStatus: "NOT_FOUND",
                ReleasedRedisSeatsCount: 0);
        }

        if (command.OutcomeType == ReservationOutcomeType.Confirmed)
        {
            var stateChanged = reservation.Confirm();
            if (!stateChanged)
            {
                logger.LogInformation(
                    "Reservation {ReservationId} is already CONFIRMED. Recording inbox idempotently.",
                    reservation.Id);
                await repository.RecordProcessedMessageAsync(messageId, ConsumerGroup, command.EventType, ct);
                return reservation.ToReconcileResult(isReconciled: true);
            }

            await repository.SaveReservationStateTransactionallyAsync(
                reservation,
                messageId,
                ConsumerGroup,
                command.EventType,
                ct);

            logger.LogInformation("Reservation {ReservationId} transitioned to CONFIRMED.", reservation.Id);
            return reservation.ToReconcileResult(isReconciled: true);
        }

        // Cancellation outcomes (CancelledByRefund or CancelledByDecline)
        var cancellationReason = !string.IsNullOrWhiteSpace(command.Reason)
            ? command.Reason!
            : command.OutcomeType == ReservationOutcomeType.CancelledByRefund
                ? "AUTO_REFUND_SEAT_COLLISION"
                : "PAYMENT_DECLINED";

        var cancelledStateChanged = reservation.Cancel(cancellationReason);
        if (!cancelledStateChanged)
        {
            logger.LogInformation(
                "Reservation {ReservationId} is already in terminal status {Status}. Recording inbox idempotently.",
                reservation.Id,
                reservation.Status);
            await repository.RecordProcessedMessageAsync(messageId, ConsumerGroup, command.EventType, ct);
            return reservation.ToReconcileResult(isReconciled: true);
        }

        await repository.SaveReservationStateTransactionallyAsync(
            reservation,
            messageId,
            ConsumerGroup,
            command.EventType,
            ct);

        var numberedSeatIds = reservation.Items
            .Where(i => i.SeatId.HasValue && i.SeatId.Value != Guid.Empty)
            .Select(i => i.SeatId!.Value)
            .ToList();

        var releasedSeats = 0;
        if (numberedSeatIds.Count > 0)
        {
            releasedSeats = await seatLockReleaser.ReleaseHeldSeatsAsync(
                reservation.EventId,
                reservation.Id,
                numberedSeatIds,
                ct);
        }

        logger.LogInformation(
            "Reservation {ReservationId} transitioned to CANCELLED (Reason: {Reason}). Released {ReleasedCount} Redis seat hold locks.",
            reservation.Id,
            reservation.CancellationReason,
            releasedSeats);

        return reservation.ToReconcileResult(isReconciled: true, releasedRedisSeatsCount: releasedSeats);
    }
}
