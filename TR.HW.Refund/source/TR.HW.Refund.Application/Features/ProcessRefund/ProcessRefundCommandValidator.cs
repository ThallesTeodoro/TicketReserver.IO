namespace TR.HW.Refund.Application.Features.ProcessRefund;

using FluentValidation;

public sealed class ProcessRefundCommandValidator : AbstractValidator<ProcessRefundCommand>
{
    public ProcessRefundCommandValidator()
    {
        RuleFor(x => x.ReservationId)
            .NotEmpty().WithMessage("ReservationId is required.");

        RuleFor(x => x.Reason)
            .NotEmpty().WithMessage("Refund reason is required.")
            .MaximumLength(150).WithMessage("Refund reason cannot exceed 150 characters.");
    }
}
