using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using RabbitMQ.Client;

namespace Platform.EventBus.Topology;

public sealed class RabbitMqTopologyHostedService(
    IConnection connection,
    ILogger<RabbitMqTopologyHostedService> logger)
    : IHostedService
{
    public async Task StartAsync(CancellationToken cancellationToken)
    {
        await using var channel =
            await connection.CreateChannelAsync(
                cancellationToken: cancellationToken);

        // 1. Exchanges
        await channel.ExchangeDeclareAsync(
            exchange: MessagingConstants.Exchanges.CatalogEvents,
            type: ExchangeType.Topic,
            durable: true,
            autoDelete: false,
            cancellationToken: cancellationToken);

        await channel.ExchangeDeclareAsync(
            exchange: MessagingConstants.Exchanges.OrdersEvents,
            type: ExchangeType.Topic,
            durable: true,
            autoDelete: false,
            cancellationToken: cancellationToken);

        // 2. Dead Letter Exchange
        await channel.ExchangeDeclareAsync(
            exchange: MessagingConstants.Exchanges.CatalogDeadLetter,
            type: ExchangeType.Direct,
            durable: true,
            autoDelete: false,
            cancellationToken: cancellationToken);

        // 3. Orders queues
        await DeclareConsumerQueueAsync(
            channel,
            MessagingConstants.Queues.OrdersCatalogSync,
            MessagingConstants.Queues.OrdersCatalogSyncDlq,
            MessagingConstants.RoutingKeys.OrdersDlq,
            cancellationToken);

        // 4. Reviews queues
        await DeclareConsumerQueueAsync(
            channel,
            MessagingConstants.Queues.ReviewsCatalogSync,
            MessagingConstants.Queues.ReviewsCatalogSyncDlq,
            MessagingConstants.RoutingKeys.ReviewsDlq,
            cancellationToken);

        logger.LogInformation(
            "RabbitMQ topology successfully declared");
    }

    private static async Task DeclareConsumerQueueAsync(
        IChannel channel,
        string queueName,
        string deadLetterQueueName,
        string deadLetterRoutingKey,
        CancellationToken cancellationToken)
    {
        var arguments = new Dictionary<string, object?>
        {
            ["x-dead-letter-exchange"] =
                MessagingConstants.Exchanges.CatalogDeadLetter,

            ["x-dead-letter-routing-key"] =
                deadLetterRoutingKey
        };

        // Основна durable-черга
        await channel.QueueDeclareAsync(
            queue: queueName,
            durable: true,
            exclusive: false,
            autoDelete: false,
            arguments: arguments,
            cancellationToken: cancellationToken);

        // Binding основної черги
        await channel.QueueBindAsync(
            queue: queueName,
            exchange: MessagingConstants.Exchanges.CatalogEvents,
            routingKey: MessagingConstants.RoutingKeys.ProductEvents,
            cancellationToken: cancellationToken);

        // Durable DLQ
        await channel.QueueDeclareAsync(
            queue: deadLetterQueueName,
            durable: true,
            exclusive: false,
            autoDelete: false,
            cancellationToken: cancellationToken);

        // Binding DLQ до DLX
        await channel.QueueBindAsync(
            queue: deadLetterQueueName,
            exchange: MessagingConstants.Exchanges.CatalogDeadLetter,
            routingKey: deadLetterRoutingKey,
            cancellationToken: cancellationToken);
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        return Task.CompletedTask;
    }
}
