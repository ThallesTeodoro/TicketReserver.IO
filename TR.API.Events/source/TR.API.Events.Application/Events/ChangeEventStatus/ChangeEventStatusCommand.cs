namespace TR.API.Events.Application.Events.ChangeEventStatus;

using FluentValidation;
using TR.API.Events.Application.Common.Messaging;
using TR.API.Events.Application.Events.Common;
using TR.API.Events.Domain.Contracts.Persistence;
using TR.API.Events.Domain.Enums;
using TR.API.Events.Domain.Exceptions;

public sealed record ChangeEventStatusCommand(Guid Id, string TargetStatus) : ICommand<EventResponse>;

public sealed class ChangeEventStatusCommandValidator : AbstractValidator<ChangeEventStatusCommand>
{
    public ChangeEventStatusCommandValidator()
    {
        RuleFor(x => x.Id).NotEmpty().WithMessage("Event Id is required.");
        RuleFor(x => x.TargetStatus).NotEmpty().WithMessage("TargetStatus is required.");
    }
}

public sealed class ChangeEventStatusCommandHandler(
    IEventRepository eventRepository,
    IUnitOfWork unitOfWork,
    IValidator<ChangeEventStatusCommand> validator) : ICommandHandler<ChangeEventStatusCommand, EventResponse>
{
    public async Task<EventResponse> HandleAsync(ChangeEventStatusCommand command, CancellationToken cancellationToken = default)
    {
        var validationResult = await validator.ValidateAsync(command, cancellationToken);
        if (!validationResult.IsValid)
        {
            throw new TR.API.Events.Domain.Exceptions.ValidationException(
                "Invalid status change request.",
                validationResult.Errors.Select(e => e.ErrorMessage));
        }

        if (!Enum.TryParse<EventStatus>(command.TargetStatus.Trim(), ignoreCase: true, out var targetStatus))
        {
            throw new DomainException($"Invalid event status '{command.TargetStatus}'.");
        }

        var evt = await eventRepository.GetByIdAsync(command.Id, cancellationToken)
            ?? throw new NotFoundException($"Event with ID '{command.Id}' was not found.");

        evt.TransitionTo(targetStatus);

        eventRepository.Update(evt);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        return evt.ToResponse();
    }
}
