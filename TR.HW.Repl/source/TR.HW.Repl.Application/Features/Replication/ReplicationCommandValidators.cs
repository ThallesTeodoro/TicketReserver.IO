using FluentValidation;
using TR.HW.Repl.Domain.Models;

namespace TR.HW.Repl.Application.Features.Replication;

public sealed class ReplicateEventChangeCommandValidator : AbstractValidator<ReplicateEventChangeCommand>
{
    public ReplicateEventChangeCommandValidator()
    {
        RuleFor(x => x.EventId)
            .NotEmpty()
            .WithMessage("EventId is required.");

        When(x => x.Operation != CdcOperation.Delete, () =>
        {
            RuleFor(x => x.Title)
                .NotEmpty()
                .WithMessage("Title is required for insert/update operations.");

            RuleFor(x => x.Slug)
                .NotEmpty()
                .WithMessage("Slug is required for insert/update operations.");

            RuleFor(x => x.Category)
                .NotEmpty()
                .WithMessage("Category is required for insert/update operations.");
        });
    }
}

public sealed class ReplicateTicketTierChangeCommandValidator : AbstractValidator<ReplicateTicketTierChangeCommand>
{
    public ReplicateTicketTierChangeCommandValidator()
    {
        RuleFor(x => x.TierId)
            .NotEmpty()
            .WithMessage("TierId is required.");

        RuleFor(x => x.EventId)
            .NotEmpty()
            .WithMessage("EventId is required.");

        When(x => x.Operation != CdcOperation.Delete, () =>
        {
            RuleFor(x => x.TierName)
                .NotEmpty()
                .WithMessage("TierName is required for tier creation/update.");

            RuleFor(x => x.Price)
                .NotNull()
                .GreaterThanOrEqualTo(0)
                .WithMessage("Valid non-negative Price is required for tier creation/update.");
        });
    }
}
