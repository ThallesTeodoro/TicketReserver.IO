namespace TR.API.Events.Application.Events.CancelEvent;

using TR.API.Events.Application.Common.Messaging;
using TR.API.Events.Application.Events.Common;
using TR.API.Events.Domain.Contracts.Persistence;
using TR.API.Events.Domain.Exceptions;

public sealed record CancelEventCommand(Guid Id) : ICommand<EventResponse>;

public sealed class CancelEventCommandHandler(
    IEventRepository eventRepository,
    IUnitOfWork unitOfWork) : ICommandHandler<CancelEventCommand, EventResponse>
{
    public async Task<EventResponse> HandleAsync(CancelEventCommand command, CancellationToken cancellationToken = default)
    {
        if (command.Id == Guid.Empty)
        {
            throw new DomainException("Event Id is required.");
        }

        var evt = await eventRepository.GetByIdAsync(command.Id, cancellationToken)
            ?? throw new NotFoundException($"Event with ID '{command.Id}' was not found.");

        evt.Cancel();

        eventRepository.Update(evt);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        return evt.ToResponse();
    }
}
