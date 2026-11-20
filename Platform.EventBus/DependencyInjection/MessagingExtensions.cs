using Microsoft.Extensions.DependencyInjection;
using Platform.EventBus.Topology;
using Platform.EventBus.Publishing;

namespace Platform.EventBus.DependencyInjection;

public static class MessagingExtensions
{
    public static IServiceCollection AddMessagingTopology(
        this IServiceCollection services)
    {
        services.AddHostedService<RabbitMqTopologyHostedService>();

        return services;
    }

    public static IServiceCollection AddEventPublisher(
        this IServiceCollection services)
    {
        services.AddSingleton<IEventPublisher, RabbitMqEventPublisher>();

        return services;
    }

}
