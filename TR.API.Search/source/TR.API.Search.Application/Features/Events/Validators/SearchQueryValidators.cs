using FluentValidation;
using TR.API.Search.Application.Features.Events.AutocompleteEvents;
using TR.API.Search.Application.Features.Events.GetEventDetails;
using TR.API.Search.Application.Features.Events.SearchEvents;

namespace TR.API.Search.Application.Features.Events.Validators;

public class SearchEventsQueryValidator : AbstractValidator<SearchEventsQuery>
{
    public SearchEventsQueryValidator()
    {
        RuleFor(x => x.Page)
            .GreaterThanOrEqualTo(1)
            .WithMessage("Page must be greater than or equal to 1.");

        RuleFor(x => x.PageSize)
            .InclusiveBetween(1, 100)
            .WithMessage("PageSize must be between 1 and 100.");

        RuleFor(x => x.MinPrice)
            .GreaterThanOrEqualTo(0)
            .When(x => x.MinPrice.HasValue)
            .WithMessage("MinPrice cannot be negative.");

        RuleFor(x => x.MaxPrice)
            .GreaterThanOrEqualTo(0)
            .When(x => x.MaxPrice.HasValue)
            .WithMessage("MaxPrice cannot be negative.");

        RuleFor(x => x)
            .Must(x => !x.MinPrice.HasValue || !x.MaxPrice.HasValue || x.MinPrice.Value <= x.MaxPrice.Value)
            .WithMessage("MinPrice cannot be greater than MaxPrice.");

        RuleFor(x => x)
            .Must(x => !x.FromDate.HasValue || !x.ToDate.HasValue || x.FromDate.Value <= x.ToDate.Value)
            .WithMessage("FromDate cannot be greater than ToDate.");

        RuleFor(x => x.Latitude)
            .InclusiveBetween(-90, 90)
            .When(x => x.Latitude.HasValue)
            .WithMessage("Latitude must be between -90 and 90.");

        RuleFor(x => x.Longitude)
            .InclusiveBetween(-180, 180)
            .When(x => x.Longitude.HasValue)
            .WithMessage("Longitude must be between -180 and 180.");

        RuleFor(x => x)
            .Must(x => (x.Latitude.HasValue && x.Longitude.HasValue) || (!x.Latitude.HasValue && !x.Longitude.HasValue))
            .WithMessage("Both Latitude and Longitude must be provided together for geo search.");

        RuleFor(x => x.RadiusKm)
            .InclusiveBetween(0.1, 500)
            .When(x => x.RadiusKm.HasValue)
            .WithMessage("RadiusKm must be between 0.1 and 500.");

        RuleFor(x => x)
            .Must(x => !x.RadiusKm.HasValue || (x.Latitude.HasValue && x.Longitude.HasValue))
            .WithMessage("Latitude and Longitude are required when RadiusKm is specified.");
    }
}

public class AutocompleteEventsQueryValidator : AbstractValidator<AutocompleteEventsQuery>
{
    public AutocompleteEventsQueryValidator()
    {
        RuleFor(x => x.Limit)
            .InclusiveBetween(1, 50)
            .WithMessage("Limit must be between 1 and 50.");
    }
}

public class GetEventByIdQueryValidator : AbstractValidator<GetEventByIdQuery>
{
    public GetEventByIdQueryValidator()
    {
        RuleFor(x => x.EventId)
            .NotEmpty()
            .WithMessage("EventId cannot be empty.");
    }
}
