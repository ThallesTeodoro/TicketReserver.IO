namespace TR.API.Reservation.Application.Reservations.CancelReservation;

using FluentValidation;

public sealed class CancelReservationCommandValidator : AbstractValidator<CancelReservationCommand>
{
    public CancelReservationCommandValidator()
    {
        RuleFor(x => x.ReservationId)
            .NotEmpty().WithMessage("ReservationId não pode ser vazio.");

        RuleFor(x => x.CustomerId)
            .NotEmpty().WithMessage("CustomerId não pode ser vazio.");
    }
}
