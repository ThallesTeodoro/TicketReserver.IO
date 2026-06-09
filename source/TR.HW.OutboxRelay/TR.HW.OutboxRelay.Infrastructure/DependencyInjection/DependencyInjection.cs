using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using TR.HW.OutboxRelay.Application.Ports;
using TR.HW.OutboxRelay.Infrastructure.Adapters.Data;
using TR.HW.OutboxRelay.Infrastructure.Adapters.Kafka;

namespace TR.HW.OutboxRelay.Infrastructure.DependencyInjection;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        // Adapters
        services.AddSingleton<IKafkaPort, KafkaAdapter>();
        services.AddScoped<IOutboxPort, OutboxRepository>();

        return services;
    }
}
