namespace TR.HW.Refund.Application;

using FluentValidation;
using Microsoft.Extensions.DependencyInjection;
using TR.HW.Refund.Application.Common.Messaging;
using TR.HW.Refund.Application.Features.ProcessRefund;

public static class DependencyInjection
{
    public static IServiceCollection AddRefundApplication(this IServiceCollection services)
    {
        services.AddScoped<IValidator<ProcessRefundCommand>, ProcessRefundCommandValidator>();
        services.AddScoped<ICommandHandler<ProcessRefundCommand, ProcessRefundResult>, ProcessRefundCommandHandler>();

        return services;
    }
}
