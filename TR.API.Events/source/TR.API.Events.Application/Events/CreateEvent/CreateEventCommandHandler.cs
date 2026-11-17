namespace TR.API.Events.Application.Events.CreateEvent;

using FluentValidation;
using TR.API.Events.Application.Common.Messaging;
using TR.API.Events.Application.Events.Common;
using TR.API.Events.Domain.Contracts.Persistence;
using TR.API.Events.Domain.Entities;
using TR.API.Events.Domain.Enums;
using TR.API.Events.Domain.Exceptions;
using TR.API.Events.Domain.ValueObjects;

public sealed class CreateEventCommandHandler(
    IEventRepository eventRepository,
    IUnitOfWork unitOfWork,
    IValidator<CreateEventCommand> validator) : ICommandHandler<CreateEventCommand, EventResponse>
{
    public async Task<EventResponse> HandleAsync(CreateEventCommand command, CancellationToken cancellationToken = default)
    {
        var validationResult = await validator.ValidateAsync(command, cancellationToken);
        if (!validationResult.IsValid)
        {
            throw new TR.API.Events.Domain.Exceptions.ValidationException(
                "Invalid event creation request.",
                validationResult.Errors.Select(e => e.ErrorMessage));
        }

        var venueExists = await eventRepository.VenueExistsAsync(command.VenueId, cancellationToken);
        if (!venueExists)
        {
            throw new NotFoundException($"Venue with ID '{command.VenueId}' was not found.");
        }

        var slugExists = await eventRepository.ExistsBySlugAsync(command.Slug, null, cancellationToken);
        if (slugExists)
        {
            throw new DomainException($"An event with slug '{command.Slug}' already exists.");
        }

        var salesWindow = new DateTimeRange(command.SalesStartAt, command.SalesEndAt, "Sales window");
        var schedule = new DateTimeRange(command.EventStartAt, command.EventEndAt, "Event schedule");

        var evt = Event.Create(
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

        if (command.TicketTiers is { Count: > 0 })
        {
            foreach (var item in command.TicketTiers)
            {
                if (!Enum.TryParse<TicketTierType>(item.TierType, ignoreCase: true, out var tierType))
                {
                    throw new DomainException($"Invalid ticket tier type '{item.TierType}'.");
                }

                var money = new Money(item.Price, item.Currency);
                var tierSalesWindow = new DateTimeRange(item.SalesStartAt, item.SalesEndAt, "Ticket tier sales window");

                evt.AddTicketTier(item.SectionId, item.Name, tierType, money, item.TotalQuantity, tierSalesWindow);
            }
        }

        await eventRepository.AddAsync(evt, cancellationToken);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        return evt.ToResponse();
    }
}
