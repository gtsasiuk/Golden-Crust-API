using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using OpenTelemetry.Trace;
using Orders.Bll.IntegrationEvents.Handlers;
using Platform.EventBus.Contracts;
using Platform.EventBus.Diagnostics;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using Platform.EventBus.Tracing;

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
    private CancellationToken _stoppingToken;

    protected override async Task ExecuteAsync(
        CancellationToken stoppingToken)
    {
        _stoppingToken = stoppingToken;

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

        using var activity = TracePropagation.StartConsumerActivity(ea, QueueName);

        activity?.SetTag("event.id", messageId);
        activity?.SetTag("event.type", eventType);
        activity?.SetTag("correlation.id", correlationId);

        activity?.SetTag("messaging.system", "rabbitmq");
        activity?.SetTag("messaging.destination.name", QueueName);
        activity?.SetTag("messaging.operation.type", "process");
        activity?.SetTag("messaging.message.id", messageId);
        activity?.SetTag("messaging.rabbitmq.message.type", eventType);

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
                        var e =
                            JsonSerializer.Deserialize<MenuItemPriceChangedEvent>(
                                ea.Body.Span,
                                JsonDefaults.Options)
                            ?? throw new JsonException(
                                "Empty price event");

                        ValidatePriceChangedEvent(e);

                        activity?.SetTag(
                            "event.menu_item_id",
                            e.MenuItemId);

                        await scope.ServiceProvider
                            .GetRequiredService<MenuItemPriceChangedHandler>()
                            .HandleAsync(e, _stoppingToken);

                        break;
                    }

                case nameof(MenuItemRenamedEvent):
                    {
                        var e =
                            JsonSerializer.Deserialize<MenuItemRenamedEvent>(
                                ea.Body.Span,
                                JsonDefaults.Options)
                            ?? throw new JsonException(
                                "Empty rename event");

                        ValidateRenamedEvent(e);

                        activity?.SetTag(
                            "event.menu_item_id",
                            e.MenuItemId);

                        await scope.ServiceProvider
                            .GetRequiredService<MenuItemRenamedHandler>()
                            .HandleAsync(e, _stoppingToken);

                        break;
                    }

                default:
                    throw new InvalidDataException(
                        $"Unsupported event type: {eventType}");
            }
        }
        catch (OperationCanceledException)
            when (_stoppingToken.IsCancellationRequested)
        {
            activity?.SetStatus(
                ActivityStatusCode.Error,
                "Message processing cancelled during shutdown");

            logger.LogInformation(
                "Message {MessageId} processing cancelled. " +
                "It will be redelivered after consumer shutdown",
                messageId);

            // Не ACK і не NACK.
            // Непідтверджене повідомлення буде повернуте RabbitMQ
            // після закриття каналу або з'єднання.
            return;
        }
        catch (Exception ex) when (IsPermanentMessageException(ex))
        {
            activity?.SetStatus(
                ActivityStatusCode.Error,
                ex.Message);

            activity?.RecordException(ex);

            logger.LogError(
                ex,
                "Permanent error in message {MessageId}, EventType: {EventType}. Sending to DLQ",
                messageId,
                eventType);

            await _channel!.BasicNackAsync(
                ea.DeliveryTag,
                multiple: false,
                requeue: false);

            return;
        }
        catch (Exception ex)
        {
            activity?.SetStatus(
                ActivityStatusCode.Error,
                ex.Message);

            activity?.RecordException(ex);

            await RetryOrDeadLetterAsync(ea, ex);

            return;
        }

        // ACK виконується тільки після успішної бізнес-обробки.
        await _channel!.BasicAckAsync(
            ea.DeliveryTag,
            multiple: false);

        logger.LogInformation(
            "Message {MessageId} acknowledged successfully. EventType: {EventType}",
            messageId,
            eventType);
    }

    private static void ValidatePriceChangedEvent(
        MenuItemPriceChangedEvent e)
    {
        if (e.EventId == Guid.Empty)
            throw new InvalidDataException("EventId is empty");

        if (e.MenuItemId <= 0)
            throw new InvalidDataException("Invalid MenuItemId");

        if (string.IsNullOrWhiteSpace(e.MenuItemName))
            throw new InvalidDataException("MenuItemName is empty");

        if (e.OldPrice < 0 || e.NewPrice < 0)
            throw new InvalidDataException("Invalid product price");

        if (e.SchemaVersion <= 0)
            throw new InvalidDataException("Invalid SchemaVersion");
    }

    private static void ValidateRenamedEvent(
        MenuItemRenamedEvent e)
    {
        if (e.EventId == Guid.Empty)
            throw new InvalidDataException("EventId is empty");

        if (e.MenuItemId <= 0)
            throw new InvalidDataException("Invalid MenuItemId");

        if (string.IsNullOrWhiteSpace(e.NewName))
            throw new InvalidDataException("NewName is empty");

        if (e.SchemaVersion <= 0)
            throw new InvalidDataException("Invalid SchemaVersion");
    }

    private static bool IsPermanentMessageException(
        Exception ex)
    {
        return ex is JsonException
            or InvalidDataException
            or ArgumentException
            or FormatException
            or NotSupportedException
            or InvalidOperationException
            or OverflowException;
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
            if (!TryParseRetryCount(value, out attempt))
            {
                logger.LogError(
                    ex,
                    "Invalid x-retry-count header for message {MessageId}. Sending to DLQ",
                    ea.BasicProperties.MessageId);

                await _channel!.BasicNackAsync(
                    ea.DeliveryTag,
                    multiple: false,
                    requeue: false);

                return;
            }
        }

        if (attempt < 0 || attempt >= MaxRetries)
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

        // Спочатку публікуємо повторне повідомлення.
        // Оригінал підтверджуємо тільки після завершення публікації.
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
            "Message {MessageId} retry {Attempt}/{MaxRetries}. EventType: {EventType}",
            ea.BasicProperties.MessageId,
            attempt + 1,
            MaxRetries,
            ea.BasicProperties.Type);
    }

    private static bool TryParseRetryCount(
        object? value,
        out int attempt)
    {
        attempt = 0;

        try
        {
            var raw = value is byte[] bytes
                ? Encoding.UTF8.GetString(bytes)
                : Convert.ToString(
                    value,
                    CultureInfo.InvariantCulture);

            return int.TryParse(
                raw,
                NumberStyles.Integer,
                CultureInfo.InvariantCulture,
                out attempt);
        }
        catch (Exception ex) when (
            ex is FormatException
            or InvalidCastException
            or OverflowException)
        {
            return false;
        }
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
            _channel.DisposeAsync()
                .AsTask()
                .GetAwaiter()
                .GetResult();

            _channel = null;
        }

        base.Dispose();
    }
}
