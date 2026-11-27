namespace TR.API.Reservation.Application.Reservations.HoldReservation;

using FluentValidation;

public class HoldReservationCommandValidator : AbstractValidator<HoldReservationCommand>
{
    public HoldReservationCommandValidator()
    {
        RuleFor(x => x.CustomerId)
            .NotEmpty().WithMessage("CustomerId não pode ser vazio.");

        RuleFor(x => x.EventId)
            .NotEmpty().WithMessage("EventId não pode ser vazio.");

        RuleFor(x => x.Items)
            .NotEmpty().WithMessage("A reserva deve conter ao menos um item.");

        RuleForEach(x => x.Items).ChildRules(item =>
        {
            item.RuleFor(i => i.TicketTierId)
                .NotEmpty().WithMessage("TicketTierId não pode ser vazio.");

            item.RuleFor(i => i.Quantity)
                .GreaterThan(0).WithMessage("A quantidade deve ser maior que zero.");

            item.RuleFor(i => i.UnitPrice)
                .GreaterThanOrEqualTo(0).WithMessage("O preço unitário não pode ser negativo.");
        });
    }
}
