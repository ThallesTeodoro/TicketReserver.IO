namespace TR.API.Events.Application.Events.UpdateEvent;

using FluentValidation;

public sealed class UpdateEventCommandValidator : AbstractValidator<UpdateEventCommand>
{
    public UpdateEventCommandValidator()
    {
        RuleFor(x => x.Id)
            .NotEmpty().WithMessage("Event Id is required.");

        RuleFor(x => x.VenueId)
            .NotEmpty().WithMessage("VenueId is required.");

        RuleFor(x => x.Title)
            .NotEmpty().WithMessage("Title is required.")
            .MaximumLength(200).WithMessage("Title cannot exceed 200 characters.");

        RuleFor(x => x.Slug)
            .NotEmpty().WithMessage("Slug is required.")
            .MaximumLength(220).WithMessage("Slug cannot exceed 220 characters.");

        RuleFor(x => x.Category)
            .NotEmpty().WithMessage("Category is required.")
            .MaximumLength(60).WithMessage("Category cannot exceed 60 characters.");

        RuleFor(x => x.SalesEndAt)
            .GreaterThan(x => x.SalesStartAt)
            .WithMessage("SalesEndAt must be strictly greater than SalesStartAt.");

        RuleFor(x => x.EventEndAt)
            .GreaterThan(x => x.EventStartAt)
            .WithMessage("EventEndAt must be strictly greater than EventStartAt.");

        RuleFor(x => x.EventStartAt)
            .GreaterThanOrEqualTo(x => x.SalesStartAt)
            .WithMessage("EventStartAt must be on or after SalesStartAt.");

        RuleFor(x => x.AgeRestriction)
            .GreaterThanOrEqualTo(0)
            .WithMessage("AgeRestriction cannot be negative.");

        RuleFor(x => x.MaxTicketsPerUser)
            .GreaterThan(0)
            .WithMessage("MaxTicketsPerUser must be greater than zero.");
    }
}
