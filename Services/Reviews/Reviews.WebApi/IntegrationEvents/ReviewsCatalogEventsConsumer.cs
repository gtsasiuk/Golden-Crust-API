using System.Text;
using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Platform.EventBus.Contracts;
using Platform.EventBus.Topology;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using Reviews.Infrastructure.IntegrationEvents;

namespace Reviews.WebApi.IntegrationEvents;

public sealed class ReviewsCatalogEventsConsumer(
    IConnection connection,
    IServiceScopeFactory scopes,
    ILogger<ReviewsCatalogEventsConsumer> logger)
    : BackgroundService
{
    private const string QueueName =
        MessagingConstants.Queues.ReviewsCatalogSync;

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

        // Перевіряємо, що спільна топологія вже створила чергу.
        await _channel.QueueDeclarePassiveAsync(
            QueueName,
            stoppingToken);

        // Обмежуємо кількість непідтверджених повідомлень.
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
            "Reviews Catalog Events Consumer started. Queue: {Queue}",
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
                "Reviews Catalog Events Consumer stopping");
        }
    }

    private async Task OnMessageAsync(
        object sender,
        BasicDeliverEventArgs ea)
    {
        try
        {
            var eventType = ea.BasicProperties.Type;

            logger.LogInformation(
                "Reviews received event {EventType}. MessageId: {MessageId}, CorrelationId: {CorrelationId}",
                eventType,
                ea.BasicProperties.MessageId,
                ea.BasicProperties.CorrelationId);

            switch (eventType)
            {
                case nameof(MenuItemRenamedEvent):
                    {
                        var e = JsonSerializer.Deserialize<MenuItemRenamedEvent>(
                            ea.Body.Span,
                            JsonDefaults.Options)
                            ?? throw new JsonException(
                                "Empty MenuItemRenamedEvent");

                        await using var scope =
                            scopes.CreateAsyncScope();

                        await scope.ServiceProvider
                            .GetRequiredService<ProductRenamedHandler>()
                            .HandleAsync(e, _stoppingToken);

                        logger.LogInformation(
                            "Product renamed event handled. EventId: {EventId}, MenuItemId: {MenuItemId}",
                            e.EventId,
                            e.MenuItemId);

                        break;
                    }

                case nameof(MenuItemPriceChangedEvent):
                    {
                        // Reviews не зберігає копію ціни товару.
                        // Тому подію ціни підтверджуємо без змін у MongoDB.
                        logger.LogDebug(
                            "Price event ignored by Reviews. MessageId: {MessageId}",
                            ea.BasicProperties.MessageId);

                        break;
                    }

                default:
                    throw new JsonException(
                        $"Unsupported event type: {eventType}");
            }

            // Підтверджуємо повідомлення тільки після успішної обробки.
            await _channel!.BasicAckAsync(
                ea.DeliveryTag,
                multiple: false);

            logger.LogInformation(
                "Reviews message {MessageId} acknowledged successfully",
                ea.BasicProperties.MessageId);
        }
        catch (JsonException ex)
        {
            // Некоректний JSON або невідомий тип події.
            // requeue:false відправляє повідомлення в DLQ.
            logger.LogError(
                ex,
                "Invalid Reviews message {MessageId}. Sending to DLQ",
                ea.BasicProperties.MessageId);

            await _channel!.BasicNackAsync(
                ea.DeliveryTag,
                multiple: false,
                requeue: false);
        }
        catch (OperationCanceledException)
            when (_stoppingToken.IsCancellationRequested)
        {
            // Під час штатної зупинки не підтверджуємо
            // незавершене повідомлення.
            logger.LogInformation(
                "Reviews message processing cancelled. MessageId: {MessageId}",
                ea.BasicProperties.MessageId);
        }
        catch (Exception ex)
        {
            await RetryOrDeadLetterAsync(ea, ex);
        }
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
                "Reviews message {MessageId} failed after {Attempts} retries. Sending to DLQ",
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

        // Повторно публікуємо повідомлення з тим самим routing key.
        await _channel!.BasicPublishAsync(
            exchange: string.Empty,
            routingKey: QueueName,
            mandatory: false,
            basicProperties: properties,
            body: ea.Body.ToArray());

        // Оригінал підтверджуємо тільки після повторної публікації.
        await _channel.BasicAckAsync(
            ea.DeliveryTag,
            multiple: false);

        logger.LogWarning(
            ex,
            "Reviews message {MessageId} retry {Attempt}/{MaxRetries}",
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
