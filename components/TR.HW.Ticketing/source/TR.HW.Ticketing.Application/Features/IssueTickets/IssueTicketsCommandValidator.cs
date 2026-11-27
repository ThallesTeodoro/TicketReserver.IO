namespace TR.HW.Ticketing.Application.Features.IssueTickets;

using FluentValidation;

public sealed class IssueTicketsCommandValidator : AbstractValidator<IssueTicketsCommand>
{
    public IssueTicketsCommandValidator()
    {
        RuleFor(x => x.ReservationId)
            .NotEmpty().WithMessage("ReservationId is required.");

        RuleFor(x => x.PaymentId)
            .NotEmpty().WithMessage("PaymentId is required.");

        RuleFor(x => x.CustomerId)
            .NotEmpty().WithMessage("CustomerId is required.");

        RuleFor(x => x.EventId)
            .NotEmpty().WithMessage("EventId is required.");

        RuleFor(x => x.Items)
            .NotNull().WithMessage("Items cannot be null.")
            .NotEmpty().WithMessage("At least one ticket item must be provided.");

        RuleForEach(x => x.Items).ChildRules(item =>
        {
            item.RuleFor(i => i.TicketTierId)
                .NotEmpty().WithMessage("TicketTierId is required.");

            item.RuleFor(i => i.UnitPrice)
                .GreaterThanOrEqualTo(0).WithMessage("UnitPrice cannot be negative.");
        });
    }
}
