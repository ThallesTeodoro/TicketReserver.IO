namespace TR.HW.Payment.Infrastructure;

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using TR.HW.Payment.Application.Interfaces;
using TR.HW.Payment.Infrastructure.Configuration;
using TR.HW.Payment.Infrastructure.Gateways;
using TR.HW.Payment.Infrastructure.Messaging;
using TR.HW.Payment.Infrastructure.Persistence;

public static class DependencyInjection
{
    public static IServiceCollection AddPaymentInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        services.Configure<PaymentDatabaseOptions>(configuration.GetSection(PaymentDatabaseOptions.SectionName));
        services.Configure<KafkaProducerOptions>(configuration.GetSection(KafkaProducerOptions.SectionName));
        services.Configure<PaymentGatewayOptions>(configuration.GetSection(PaymentGatewayOptions.SectionName));

        services.AddSingleton<IDbConnectionFactory, NpgsqlConnectionFactory>();
        services.AddScoped<IPaymentRepository, PaymentRepository>();
        services.AddScoped<IPaymentGatewayService, PaymentGatewayService>();
        services.AddSingleton<IKafkaPaymentProducer, KafkaPaymentProducer>();

        return services;
    }
}
