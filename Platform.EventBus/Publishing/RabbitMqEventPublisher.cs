
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Platform.EventBus.Contracts;
using RabbitMQ.Client;

namespace Platform.EventBus.Publishing;

public sealed class RabbitMqEventPublisher(
    IConnection connection,
    ILogger<RabbitMqEventPublisher> logger)
    : IEventPublisher
{
    public async Task PublishAsync<T>(
        T @event,
        string exchange,
        string routingKey,
        CancellationToken ct = default)
        where T : IntegrationEvent
    {
        await using var channel =
            await connection.CreateChannelAsync(
                cancellationToken: ct);

        var headers = new Dictionary<string, object?>
        {
            ["x-event-type"] = typeof(T).Name,
            ["x-schema-version"] = @event.SchemaVersion,
            ["x-occurred-at"] = @event.OccurredAt.ToString("O")
        };

        if (!string.IsNullOrWhiteSpace(@event.CorrelationId))
        {
            headers["x-correlation-id"] = @event.CorrelationId;
        }

        var properties = new BasicProperties
        {
            Persistent = true,
            ContentType = "application/json",
            ContentEncoding = "utf-8",
            MessageId = @event.EventId.ToString(),
            Type = typeof(T).Name,
            CorrelationId = @event.CorrelationId,
            Headers = headers
        };

        var body = JsonSerializer.SerializeToUtf8Bytes(
            @event,
            JsonDefaults.Options);

        await channel.BasicPublishAsync(
            exchange: exchange,
            routingKey: routingKey,
            mandatory: false,
            basicProperties: properties,
            body: body,
            cancellationToken: ct);

        logger.LogInformation(
            "Published event {EventType} with EventId {EventId}, CorrelationId {CorrelationId} to {Exchange}/{RoutingKey}",
            typeof(T).Name,
            @event.EventId,
            @event.CorrelationId,
            exchange,
            routingKey);
    }
}
