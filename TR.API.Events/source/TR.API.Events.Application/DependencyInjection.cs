namespace TR.API.Events.Application;

using System.Reflection;
using FluentValidation;
using Microsoft.Extensions.DependencyInjection;
using TR.API.Events.Application.Common.Messaging;

public static class DependencyInjection
{
    public static IServiceCollection AddApplicationServices(this IServiceCollection services)
    {
        var assembly = typeof(DependencyInjection).Assembly;

        services.AddSingleton<ISender, DefaultSender>();
        services.AddValidatorsFromAssembly(assembly);

        RegisterHandlers(services, assembly);

        return services;
    }

    private static void RegisterHandlers(IServiceCollection services, Assembly assembly)
    {
        var types = assembly.GetTypes().Where(t => t.IsClass && !t.IsAbstract);

        foreach (var type in types)
        {
            foreach (var iface in type.GetInterfaces())
            {
                if (iface.IsGenericType)
                {
                    var genericDef = iface.GetGenericTypeDefinition();
                    if (genericDef == typeof(ICommandHandler<>) ||
                        genericDef == typeof(ICommandHandler<,>) ||
                        genericDef == typeof(IQueryHandler<,>))
                    {
                        services.AddTransient(iface, type);
                    }
                }
            }
        }
    }
}
