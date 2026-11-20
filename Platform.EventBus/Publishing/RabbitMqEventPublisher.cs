using System.Diagnostics;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Platform.EventBus.Contracts;
using Platform.EventBus.Diagnostics;
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
        using var activity =
            EventBusDiagnostics.ActivitySource.StartActivity(
                $"publish {typeof(T).Name}",
                ActivityKind.Producer);

        var currentActivity = activity ?? Activity.Current;

        activity?.SetTag("messaging.system", "rabbitmq");
        activity?.SetTag("messaging.destination.name", exchange);
        activity?.SetTag("messaging.rabbitmq.destination.routing_key", routingKey);
        activity?.SetTag("messaging.message.id", @event.EventId.ToString());

        var correlationId =
            @event.CorrelationId ??
            currentActivity?.TraceId.ToString();

        await using var channel =
            await connection.CreateChannelAsync(
                cancellationToken: ct);

        var headers = new Dictionary<string, object?>
        {
            ["x-event-type"] = typeof(T).Name,
            ["x-schema-version"] = @event.SchemaVersion,
            ["x-occurred-at"] = @event.OccurredAt.ToString("O")
        };

        // W3C Trace Context
        if (currentActivity is not null)
        {
            headers["traceparent"] = currentActivity.Id;

            if (!string.IsNullOrWhiteSpace(
                    currentActivity.TraceStateString))
            {
                headers["tracestate"] =
                    currentActivity.TraceStateString;
            }
        }

        if (!string.IsNullOrWhiteSpace(correlationId))
        {
            headers["x-correlation-id"] = correlationId;
        }

        var properties = new BasicProperties
        {
            Persistent = true,
            ContentType = "application/json",
            ContentEncoding = "utf-8",
            MessageId = @event.EventId.ToString(),
            Type = typeof(T).Name,
            CorrelationId = correlationId,
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
            "Published event {EventType}. EventId: {EventId}, TraceId: {TraceId}, CorrelationId: {CorrelationId}, Exchange: {Exchange}, RoutingKey: {RoutingKey}",
            typeof(T).Name,
            @event.EventId,
            currentActivity?.TraceId.ToString(),
            correlationId,
            exchange,
            routingKey);
    }
}
