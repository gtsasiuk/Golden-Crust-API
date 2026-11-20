using System.Diagnostics;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Orders.Bll.IntegrationEvents.Handlers;
using Platform.EventBus.Contracts;
using Platform.EventBus.Diagnostics;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using OpenTelemetry.Trace;

namespace Orders.Api.IntegrationEvents;

public sealed class OrdersCatalogEventsConsumer(
    IConnection connection,
    IServiceScopeFactory scopes,
    ILogger<OrdersCatalogEventsConsumer> logger)
    : BackgroundService
{
    private const string QueueName = "orders.catalog-sync";
    private const int PrefetchCount = 10;
    private const int MaxRetries = 3;

    private IChannel? _channel;
    private string? _consumerTag;

    protected override async Task ExecuteAsync(
        CancellationToken stoppingToken)
    {
        _channel = await connection.CreateChannelAsync(
            cancellationToken: stoppingToken);

        await _channel.QueueDeclarePassiveAsync(
            QueueName,
            stoppingToken);

        await _channel.BasicQosAsync(
            prefetchSize: 0,
            prefetchCount: PrefetchCount,
            global: false,
            cancellationToken: stoppingToken);

        var consumer = new AsyncEventingBasicConsumer(_channel);

        consumer.ReceivedAsync += OnMessageAsync;

        _consumerTag = await _channel.BasicConsumeAsync(
            queue: QueueName,
            autoAck: false,
            consumer: consumer,
            cancellationToken: stoppingToken);

        logger.LogInformation(
            "Orders Catalog Events Consumer started. Queue: {Queue}",
            QueueName);

        try
        {
            await Task.Delay(
                Timeout.Infinite,
                stoppingToken);
        }
        catch (OperationCanceledException)
            when (stoppingToken.IsCancellationRequested)
        {
            logger.LogInformation(
                "Orders Catalog Events Consumer stopping");
        }
    }

    private async Task OnMessageAsync(
        object sender,
        BasicDeliverEventArgs ea)
    {
        var eventType = ea.BasicProperties.Type ?? "Unknown";
        var messageId = ea.BasicProperties.MessageId;
        var correlationId = GetCorrelationId(ea);

        // Відновлюємо контекст W3C Trace Context.
        var parentContext = ExtractActivityContext(ea);

        // Створюємо окрему Activity для обробки повідомлення.
        using var activity =
            EventBusDiagnostics.ActivitySource.StartActivity(
                $"process {eventType}",
                ActivityKind.Consumer,
                parentContext);

        // Додаємо діагностичну інформацію.
        activity?.SetTag("messaging.system", "rabbitmq");
        activity?.SetTag("messaging.destination.name", QueueName);
        activity?.SetTag("messaging.operation.type", "process");
        activity?.SetTag("messaging.message.id", messageId);
        activity?.SetTag("messaging.rabbitmq.message.type", eventType);
        activity?.SetTag("event.id", messageId);
        activity?.SetTag("event.type", eventType);
        activity?.SetTag("correlation.id", correlationId);

        try
        {
            logger.LogInformation(
                "Received event {EventType}, MessageId: {MessageId}, CorrelationId: {CorrelationId}, TraceId: {TraceId}",
                eventType,
                messageId,
                correlationId,
                Activity.Current?.TraceId.ToString());

            await using var scope = scopes.CreateAsyncScope();

            switch (eventType)
            {
                case nameof(MenuItemPriceChangedEvent):
                    {
                        var e = JsonSerializer.Deserialize<MenuItemPriceChangedEvent>(
                            ea.Body.Span,
                            JsonDefaults.Options)
                            ?? throw new JsonException("Empty price event");

                        activity?.SetTag("event.menu_item_id", e.MenuItemId);

                        await scope.ServiceProvider
                            .GetRequiredService<MenuItemPriceChangedHandler>()
                            .HandleAsync(e);

                        break;
                    }

                case nameof(MenuItemRenamedEvent):
                    {
                        var e = JsonSerializer.Deserialize<MenuItemRenamedEvent>(
                            ea.Body.Span,
                            JsonDefaults.Options)
                            ?? throw new JsonException("Empty rename event");

                        activity?.SetTag("event.menu_item_id", e.MenuItemId);

                        await scope.ServiceProvider
                            .GetRequiredService<MenuItemRenamedHandler>()
                            .HandleAsync(e);

                        break;
                    }

                default:
                    throw new JsonException(
                        $"Unsupported event type: {eventType}");
            }

            await _channel!.BasicAckAsync(
                ea.DeliveryTag,
                multiple: false);

            logger.LogInformation(
                "Message {MessageId} acknowledged successfully",
                messageId);
        }
        catch (JsonException ex)
        {
            activity?.SetStatus(
                ActivityStatusCode.Error,
                ex.Message);

            activity?.RecordException(ex);

            logger.LogError(
                ex,
                "Invalid message {MessageId}. Sending to DLQ",
                messageId);

            await _channel!.BasicNackAsync(
                ea.DeliveryTag,
                multiple: false,
                requeue: false);
        }
        catch (Exception ex)
        {
            activity?.SetStatus(
                ActivityStatusCode.Error,
                ex.Message);

            activity?.RecordException(ex);

            await RetryOrDeadLetterAsync(ea, ex);
        }
    }

    private static ActivityContext ExtractActivityContext(
        BasicDeliverEventArgs ea)
    {
        var headers = ea.BasicProperties.Headers;

        if (headers is null)
            return default;

        var traceParent = GetHeaderValue(
            headers,
            "traceparent");

        var traceState = GetHeaderValue(
            headers,
            "tracestate");

        if (string.IsNullOrWhiteSpace(traceParent))
            return default;

        if (ActivityContext.TryParse(
                traceParent,
                traceState,
                out var context))
        {
            return context;
        }

        return default;
    }

    private static string? GetCorrelationId(
        BasicDeliverEventArgs ea)
    {
        if (!string.IsNullOrWhiteSpace(
                ea.BasicProperties.CorrelationId))
        {
            return ea.BasicProperties.CorrelationId;
        }

        return ea.BasicProperties.Headers is { } headers
            ? GetHeaderValue(headers, "x-correlation-id")
            : null;
    }

    private static string? GetHeaderValue(
        IDictionary<string, object?> headers,
        string key)
    {
        if (!headers.TryGetValue(key, out var value))
            return null;

        return value switch
        {
            byte[] bytes => Encoding.UTF8.GetString(bytes),
            string text => text,
            _ => value?.ToString()
        };
    }

    private async Task RetryOrDeadLetterAsync(
        BasicDeliverEventArgs ea,
        Exception ex)
    {
        var headers = ea.BasicProperties.Headers
            ?? new Dictionary<string, object?>();

        var attempt = 0;

        if (headers.TryGetValue("x-retry-count", out var value))
        {
            var raw = value is byte[] bytes
                ? Encoding.UTF8.GetString(bytes)
                : value?.ToString();

            int.TryParse(raw, out attempt);
        }

        if (attempt >= MaxRetries)
        {
            logger.LogError(
                ex,
                "Message {MessageId} failed after {Attempts} retries. Sending to DLQ",
                ea.BasicProperties.MessageId,
                attempt);

            await _channel!.BasicNackAsync(
                ea.DeliveryTag,
                multiple: false,
                requeue: false);

            return;
        }

        var retryHeaders = new Dictionary<string, object?>(headers)
        {
            ["x-retry-count"] = attempt + 1
        };

        var properties = new BasicProperties
        {
            Persistent = true,
            ContentType = ea.BasicProperties.ContentType,
            ContentEncoding = ea.BasicProperties.ContentEncoding,
            MessageId = ea.BasicProperties.MessageId,
            CorrelationId = ea.BasicProperties.CorrelationId,
            Type = ea.BasicProperties.Type,
            Headers = retryHeaders
        };

        await _channel!.BasicPublishAsync(
            exchange: string.Empty,
            routingKey: QueueName,
            mandatory: false,
            basicProperties: properties,
            body: ea.Body.ToArray());

        await _channel.BasicAckAsync(
            ea.DeliveryTag,
            multiple: false);

        logger.LogWarning(
            ex,
            "Message {MessageId} retry {Attempt}/{MaxRetries}",
            ea.BasicProperties.MessageId,
            attempt + 1,
            MaxRetries);
    }

    public override async Task StopAsync(
        CancellationToken cancellationToken)
    {
        if (_channel is { IsOpen: true } &&
            _consumerTag is not null)
        {
            await _channel.BasicCancelAsync(
                _consumerTag,
                cancellationToken: cancellationToken);
        }

        await base.StopAsync(cancellationToken);
    }

    public override void Dispose()
    {
        if (_channel is not null)
        {
            _channel.DisposeAsync().AsTask().GetAwaiter().GetResult();
            _channel = null;
        }

        base.Dispose();
    }
}
