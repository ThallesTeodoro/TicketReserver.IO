using FluentValidation;
using Microsoft.Extensions.DependencyInjection;
using TR.HW.Notification.Application.Common.Messaging;
using TR.HW.Notification.Application.Features.SendCustomerNotification;

namespace TR.HW.Notification.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        services.AddScoped<IValidator<SendCustomerNotificationCommand>, SendCustomerNotificationCommandValidator>();
        services.AddScoped<ICommandHandler<SendCustomerNotificationCommand, SendCustomerNotificationResult>, SendCustomerNotificationCommandHandler>();

        return services;
    }
}
