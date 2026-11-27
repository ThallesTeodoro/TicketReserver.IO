using FluentValidation;

namespace TR.HW.Notification.Application.Features.SendCustomerNotification;

public class SendCustomerNotificationCommandValidator : AbstractValidator<SendCustomerNotificationCommand>
{
    public SendCustomerNotificationCommandValidator()
    {
        RuleFor(x => x.ReservationId)
            .NotEmpty()
            .WithMessage("ReservationId is required.");

        RuleFor(x => x.CustomerId)
            .NotEmpty()
            .WithMessage("CustomerId is required.");

        RuleFor(x => x.CustomerEmail)
            .NotEmpty()
            .WithMessage("CustomerEmail is required.")
            .EmailAddress()
            .WithMessage("Valid email format is required.");

        RuleFor(x => x.SourceEventMessageId)
            .NotEmpty()
            .WithMessage("SourceEventMessageId is required.");

        RuleFor(x => x.NotificationType)
            .IsInEnum()
            .WithMessage("Valid NotificationType is required.");
    }
}
