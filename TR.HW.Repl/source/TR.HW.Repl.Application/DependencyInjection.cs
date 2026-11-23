using FluentValidation;
using Microsoft.Extensions.DependencyInjection;
using TR.HW.Repl.Application.Common.Messaging;
using TR.HW.Repl.Application.Features.Replication;

namespace TR.HW.Repl.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        services.AddScoped<IValidator<ReplicateEventChangeCommand>, ReplicateEventChangeCommandValidator>();
        services.AddScoped<IValidator<ReplicateTicketTierChangeCommand>, ReplicateTicketTierChangeCommandValidator>();

        services.AddScoped<ICommandHandler<ReplicateEventChangeCommand, ReplicationResult>, ReplicateEventChangeCommandHandler>();
        services.AddScoped<ICommandHandler<ReplicateTicketTierChangeCommand, ReplicationResult>, ReplicateTicketTierChangeCommandHandler>();

        return services;
    }
}
