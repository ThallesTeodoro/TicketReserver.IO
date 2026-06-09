using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using TR.HW.OutboxRelay.Application.Common.Config;
using TR.HW.OutboxRelay.Application.UseCases.RelayMessages;

namespace TR.HW.OutboxRelay.Application.DependencyInjection;

public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services, IConfiguration configuration)
    {
        // Configurações (IOptions)
        services.Configure<OutboxSettings>(configuration.GetSection(OutboxSettings.SectionName));

        // Use Cases
        services.AddScoped<RelayMessagesUseCase>();

        return services;
    }
}
