namespace TR.HW.Payment.Application.Features.ProcessPayment;

using FluentValidation;

public sealed class ProcessPaymentCommandValidator : AbstractValidator<ProcessPaymentCommand>
{
    public ProcessPaymentCommandValidator()
    {
        RuleFor(x => x.ReservationId)
            .NotEmpty().WithMessage("O identificador da reserva (ReservationId) é obrigatório.");

        RuleFor(x => x.CustomerId)
            .NotEmpty().WithMessage("O identificador do cliente (CustomerId) é obrigatório.");

        RuleFor(x => x.TotalAmount)
            .GreaterThan(0).WithMessage("O valor total do pagamento deve ser estritamente maior que zero.");

        RuleFor(x => x.Currency)
            .NotEmpty().WithMessage("A moeda deve ser informada.")
            .Length(3).WithMessage("O código da moeda deve possuir exatamente 3 caracteres ISO (ex: BRL).");
    }
}
