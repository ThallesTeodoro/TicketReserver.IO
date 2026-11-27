namespace TR.HW.ResCompensator.Application.Features.ReconcileReservation;

using TR.HW.ResCompensator.Application.Common.Messaging;
using TR.HW.ResCompensator.Domain.Enums;

public sealed record ReconcileReservationOutcomeCommand(
    Guid ReservationId,
    ReservationOutcomeType OutcomeType,
    string EventType,
    string? Reason = null,
    string? MessageId = null,
    string? Traceparent = null) : ICommand<ReconcileReservationOutcomeResult>;

public sealed record ReconcileReservationOutcomeResult(
    bool IsReconciled,
    Guid ReservationId,
    string FinalStatus,
    int ReleasedRedisSeatsCount,
    string? CancellationReason = null);
