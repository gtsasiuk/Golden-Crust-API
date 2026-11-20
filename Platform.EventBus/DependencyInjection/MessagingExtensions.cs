using Microsoft.Extensions.DependencyInjection;
using Platform.EventBus.Topology;

namespace Platform.EventBus.DependencyInjection;

public static class MessagingExtensions
{
    public static IServiceCollection AddMessagingTopology(
        this IServiceCollection services)
    {
        services.AddHostedService<RabbitMqTopologyHostedService>();

        return services;
    }
}
