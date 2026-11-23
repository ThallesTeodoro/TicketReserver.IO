namespace TR.HW.ResCompensator.Application.Features.ReconcileReservation;

using TR.HW.ResCompensator.Domain.Entities;
using TR.HW.ResCompensator.Domain.Enums;

public static class ReconcileReservationMappingExtensions
{
    public static string ToDatabaseStatusString(this ReservationStatus status)
    {
        return status switch
        {
            ReservationStatus.Confirmed => "CONFIRMED",
            ReservationStatus.Cancelled => "CANCELLED",
            ReservationStatus.Expired => "EXPIRED",
            ReservationStatus.ProcessingPayment => "PROCESSING_PAYMENT",
            _ => "AWAITING_PAYMENT"
        };
    }

    public static ReconcileReservationOutcomeResult ToReconcileResult(
        this ReservationState reservation,
        bool isReconciled,
        int releasedRedisSeatsCount = 0)
    {
        return new ReconcileReservationOutcomeResult(
            IsReconciled: isReconciled,
            ReservationId: reservation.Id,
            FinalStatus: reservation.Status.ToDatabaseStatusString(),
            ReleasedRedisSeatsCount: releasedRedisSeatsCount,
            CancellationReason: reservation.CancellationReason);
    }
}
