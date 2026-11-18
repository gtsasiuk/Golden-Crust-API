using Microsoft.Extensions.Logging.Abstractions;

namespace Platform.Aggregator.Resilience;

public static class ResilienceLog
{
    private static ILoggerFactory _factory = NullLoggerFactory.Instance;

    public static void Init(ILoggerFactory factory) => _factory = factory;

    public static ILogger For(string client) => _factory.CreateLogger($"Resilience.{client}");
}