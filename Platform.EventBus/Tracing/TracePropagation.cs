using System.Diagnostics;
using System.Text;
using OpenTelemetry;
using OpenTelemetry.Context.Propagation;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;

namespace Platform.EventBus.Tracing;

public static class TracePropagation
{
    public static readonly ActivitySource Source =
        new("Platform.Messaging");

    private static readonly TextMapPropagator Propagator =
        Propagators.DefaultTextMapPropagator;

    private const string CorrelationHeader = "correlation-id";

    // Передавання Trace Context у заголовки RabbitMQ.
    public static void Inject(
        BasicProperties properties,
        string? correlationId = null)
    {
        if (Activity.Current is not { } activity)
            return;

        properties.Headers ??= new Dictionary<string, object?>();

        Propagator.Inject(
            new PropagationContext(
                activity.Context,
                Baggage.Current),
            properties.Headers,
            (headers, key, value) =>
            {
                headers[key] = Encoding.UTF8.GetBytes(value);
            });

        // CorrelationId передається окремо від traceparent.
        correlationId ??= activity.TraceId.ToString();

        properties.Headers[CorrelationHeader] =
            Encoding.UTF8.GetBytes(correlationId);

        properties.CorrelationId = correlationId;
    }

    // Відновлення контексту під час отримання повідомлення.
    public static Activity? StartConsumerActivity(
        BasicDeliverEventArgs ea,
        string queue)
    {
        var parent = Propagator.Extract(
            default,
            ea.BasicProperties.Headers,
            (headers, key) =>
            {
                if (headers is null ||
                    !headers.TryGetValue(key, out var value))
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

        activity?.SetTag("messaging.system", "rabbitmq");
        activity?.SetTag("messaging.destination.name", queue);
        activity?.SetTag(
            "messaging.message.id",
            ea.BasicProperties.MessageId);

        activity?.SetTag(
            "messaging.operation.type",
            "process");

        activity?.SetTag(
            "messaging.event.type",
            ea.BasicProperties.Type);

        return activity;
    }
}
