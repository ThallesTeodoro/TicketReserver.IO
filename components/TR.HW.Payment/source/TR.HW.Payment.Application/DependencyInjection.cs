namespace TR.HW.Payment.Application;

using FluentValidation;
using Microsoft.Extensions.DependencyInjection;
using TR.HW.Payment.Application.Common.Messaging;
using TR.HW.Payment.Application.Features.ProcessPayment;

public static class DependencyInjection
{
    public static IServiceCollection AddPaymentApplication(this IServiceCollection services)
    {
        services.AddScoped<IValidator<ProcessPaymentCommand>, ProcessPaymentCommandValidator>();
        services.AddScoped<ICommandHandler<ProcessPaymentCommand, ProcessPaymentResult>, ProcessPaymentCommandHandler>();

        return services;
    }
}
