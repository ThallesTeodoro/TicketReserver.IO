namespace TR.API.Reservation.Application.Reservations.CheckoutReservation;

using FluentValidation;

public class CheckoutReservationCommandValidator : AbstractValidator<CheckoutReservationCommand>
{
    public CheckoutReservationCommandValidator()
    {
        RuleFor(x => x.ReservationId)
            .NotEmpty().WithMessage("ReservationId não pode ser vazio.");

        RuleFor(x => x.CustomerId)
            .NotEmpty().WithMessage("CustomerId não pode ser vazio.");

        RuleFor(x => x.PaymentDetails)
            .NotNull().WithMessage("Os detalhes de pagamento são obrigatórios.");

        When(x => x.PaymentDetails is not null, () =>
        {
            RuleFor(x => x.PaymentDetails.PaymentMethod)
                .NotEmpty().WithMessage("PaymentMethod é obrigatório.")
                .Must(m => m == "CREDIT_CARD" || m == "PIX" || m == "DEBIT_CARD")
                .WithMessage("Método de pagamento inválido. Valores aceitos: CREDIT_CARD, PIX, DEBIT_CARD.");

            RuleFor(x => x.PaymentDetails.Installments)
                .GreaterThanOrEqualTo(1).WithMessage("O número de parcelas deve ser no mínimo 1.");
        });
    }
}
