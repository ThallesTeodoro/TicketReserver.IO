namespace TR.HW.ResCompensator.Application.Features.ReconcileReservation;

using FluentValidation;

public sealed class ReconcileReservationOutcomeCommandValidator : AbstractValidator<ReconcileReservationOutcomeCommand>
{
    public ReconcileReservationOutcomeCommandValidator()
    {
        RuleFor(x => x.ReservationId)
            .NotEmpty().WithMessage("ReservationId is required.");

        RuleFor(x => x.OutcomeType)
            .IsInEnum().WithMessage("Invalid ReservationOutcomeType.");

        RuleFor(x => x.EventType)
            .NotEmpty().WithMessage("EventType is required.");
    }
}
