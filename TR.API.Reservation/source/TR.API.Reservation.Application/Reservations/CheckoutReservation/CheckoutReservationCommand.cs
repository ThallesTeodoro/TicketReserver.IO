namespace TR.API.Reservation.Application.Reservations.CheckoutReservation;

using TR.API.Reservation.Application.Common.Messaging;

public record CheckoutReservationCommand(
    Guid ReservationId,
    Guid CustomerId,
    CheckoutPaymentDetailsDto PaymentDetails) : ICommand<CheckoutReservationResponse>;
