namespace TR.API.Reservation.Application.Reservations.CheckoutReservation;

public record CheckoutPaymentDetailsDto(
    string PaymentMethod,
    string? CardToken = null,
    int Installments = 1,
    string? CustomerDocument = null);

public record CheckoutReservationResponse(
    Guid ReservationId,
    string Status,
    decimal TotalAmount,
    string Currency,
    DateTime PaymentAttemptedAt);
