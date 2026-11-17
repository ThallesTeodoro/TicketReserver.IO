namespace TR.API.Events.Application.Events.UpdateEvent;

using FluentValidation;
using TR.API.Events.Application.Common.Messaging;
using TR.API.Events.Application.Events.Common;
using TR.API.Events.Domain.Contracts.Persistence;
using TR.API.Events.Domain.Exceptions;
using TR.API.Events.Domain.ValueObjects;

public sealed class UpdateEventCommandHandler(
    IEventRepository eventRepository,
    IUnitOfWork unitOfWork,
    IValidator<UpdateEventCommand> validator) : ICommandHandler<UpdateEventCommand, EventResponse>
{
    public async Task<EventResponse> HandleAsync(UpdateEventCommand command, CancellationToken cancellationToken = default)
    {
        var validationResult = await validator.ValidateAsync(command, cancellationToken);
        if (!validationResult.IsValid)
        {
            throw new TR.API.Events.Domain.Exceptions.ValidationException(
                "Invalid event update request.",
                validationResult.Errors.Select(e => e.ErrorMessage));
        }

        var evt = await eventRepository.GetByIdAsync(command.Id, cancellationToken)
            ?? throw new NotFoundException($"Event with ID '{command.Id}' was not found.");

        var venueExists = await eventRepository.VenueExistsAsync(command.VenueId, cancellationToken);
        if (!venueExists)
        {
            throw new NotFoundException($"Venue with ID '{command.VenueId}' was not found.");
        }

        var slugExists = await eventRepository.ExistsBySlugAsync(command.Slug, command.Id, cancellationToken);
        if (slugExists)
        {
            throw new DomainException($"An event with slug '{command.Slug}' already exists.");
        }

        var salesWindow = new DateTimeRange(command.SalesStartAt, command.SalesEndAt, "Sales window");
        var schedule = new DateTimeRange(command.EventStartAt, command.EventEndAt, "Event schedule");

        evt.UpdateDetails(
            command.VenueId,
            command.Title,
            command.Slug,
            command.Description,
            command.Category,
            salesWindow,
            schedule,
            command.AgeRestriction,
            command.BannerUrl,
            command.MaxTicketsPerUser);

        eventRepository.Update(evt);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        return evt.ToResponse();
    }
}
