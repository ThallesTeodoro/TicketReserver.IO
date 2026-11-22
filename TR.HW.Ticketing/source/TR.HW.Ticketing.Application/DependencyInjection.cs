namespace TR.HW.Ticketing.Application;

using FluentValidation;
using Microsoft.Extensions.DependencyInjection;
using TR.HW.Ticketing.Application.Common.Messaging;
using TR.HW.Ticketing.Application.Features.IssueTickets;

public static class DependencyInjection
{
    public static IServiceCollection AddTicketingApplication(this IServiceCollection services)
    {
        services.AddScoped<IValidator<IssueTicketsCommand>, IssueTicketsCommandValidator>();
        services.AddScoped<ICommandHandler<IssueTicketsCommand, IssueTicketsResult>, IssueTicketsCommandHandler>();

        return services;
    }
}
