using System.Diagnostics;
using System.Text;
using OpenTelemetry;
using OpenTelemetry.Context.Propagation;
using Platform.EventBus.Diagnostics;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;

namespace Platform.EventBus.Tracing;

public static class TracePropagation
{
    // Використовуємо єдиний ActivitySource для всієї системи.
    public static ActivitySource Source =>
        EventBusDiagnostics.ActivitySource;

    private static readonly TextMapPropagator Propagator =
        Propagators.DefaultTextMapPropagator;

    private const string CorrelationHeader = "x-correlation-id";

    // Передавання контексту трасування в RabbitMQ.
    public static void Inject(
        BasicProperties properties,
        string? correlationId = null)
    {
        properties.Headers ??= new Dictionary<string, object?>();

        var activity = Activity.Current;

        if (activity is not null)
        {
            var context = new PropagationContext(
                activity.Context,
                Baggage.Current);

            Propagator.Inject(
                context,
                properties.Headers,
                (headers, key, value) =>
                {
                    headers[key] = Encoding.UTF8.GetBytes(value);
                });
        }

        // CorrelationId є окремим бізнес-ідентифікатором.
        if (!string.IsNullOrWhiteSpace(correlationId))
        {
            properties.Headers[CorrelationHeader] =
                Encoding.UTF8.GetBytes(correlationId);

            properties.CorrelationId = correlationId;
        }
    }

    // Відновлення контексту під час отримання повідомлення.
    public static Activity? StartConsumerActivity(
        BasicDeliverEventArgs ea,
        string queue)
    {
        var headers = ea.BasicProperties.Headers;

        var parent = Propagator.Extract(
            default,
            headers,
            (carrier, key) =>
            {
                if (carrier is null ||
                    !carrier.TryGetValue(key, out var value))
                {
                    return [];
                }

                return value switch
                {
                    byte[] bytes =>
                        [Encoding.UTF8.GetString(bytes)],

                    string text =>
                        [text],

                    _ => []
                };
            });

        var activity = Source.StartActivity(
            $"{queue} process",
            ActivityKind.Consumer,
            parent.ActivityContext);

        activity?.SetTag(
            "messaging.system",
            "rabbitmq");

        activity?.SetTag(
            "messaging.destination.name",
            queue);

        activity?.SetTag(
            "messaging.operation.type",
            "process");

        activity?.SetTag(
            "messaging.message.id",
            ea.BasicProperties.MessageId);

        activity?.SetTag(
            "messaging.rabbitmq.message.type",
            ea.BasicProperties.Type);

        activity?.SetTag(
            "messaging.correlation.id",
            ea.BasicProperties.CorrelationId);

        return activity;
    }
}
