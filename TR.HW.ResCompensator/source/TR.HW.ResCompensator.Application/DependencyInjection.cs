namespace TR.HW.ResCompensator.Application;

using FluentValidation;
using Microsoft.Extensions.DependencyInjection;
using TR.HW.ResCompensator.Application.Common.Messaging;
using TR.HW.ResCompensator.Application.Features.ReconcileReservation;

public static class DependencyInjection
{
    public static IServiceCollection AddReservationCompensationApplication(this IServiceCollection services)
    {
        services.AddScoped<IValidator<ReconcileReservationOutcomeCommand>, ReconcileReservationOutcomeCommandValidator>();
        services.AddScoped<ICommandHandler<ReconcileReservationOutcomeCommand, ReconcileReservationOutcomeResult>, ReconcileReservationOutcomeCommandHandler>();

        return services;
    }
}
