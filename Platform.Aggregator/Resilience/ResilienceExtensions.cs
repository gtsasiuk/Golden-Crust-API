using Microsoft.Extensions.Http.Resilience;
using Polly;

namespace Platform.Aggregator.Resilience;

public static class ResilienceExtensions
{
    public static IHttpClientBuilder AddProfiledResilience(this IHttpClientBuilder client, ResilienceProfile p)
    {
#pragma warning disable EXTEXP0001
        client.RemoveAllResilienceHandlers();
#pragma warning restore EXTEXP0001

        client.AddStandardResilienceHandler(o =>
        {
            o.Retry.MaxRetryAttempts = p.HttpRetries;
            o.Retry.Delay = p.Backoff;
            o.Retry.BackoffType = DelayBackoffType.Exponential;
            o.Retry.UseJitter = true;
            o.Retry.DisableForUnsafeHttpMethods();

            o.CircuitBreaker.FailureRatio = p.FailureRatio;
            o.CircuitBreaker.MinimumThroughput = p.MinimumThroughput;
            o.CircuitBreaker.SamplingDuration = p.SamplingDuration;
            o.CircuitBreaker.BreakDuration = p.BreakDuration;

            o.CircuitBreaker.OnOpened = args =>
            {
                ResilienceLog.For(p.Name).LogError(
                    "Circuit for {Client} ({Role}) OPENED for {BreakSeconds} s, last failure: {Failure}",
                    p.Name, p.Role, args.BreakDuration.TotalSeconds,
                    args.Outcome.Exception?.GetType().Name ?? "HTTP status");
                return ValueTask.CompletedTask;
            };
            o.CircuitBreaker.OnHalfOpened = _ =>
            {
                ResilienceLog.For(p.Name).LogInformation("Circuit for {Client} is HALF-OPEN, sending a probe request", p.Name);
                return ValueTask.CompletedTask;
            };
            o.CircuitBreaker.OnClosed = _ =>
            {
                ResilienceLog.For(p.Name).LogInformation("Circuit for {Client} CLOSED, traffic restored", p.Name);
                return ValueTask.CompletedTask;
            };

            o.AttemptTimeout.Timeout = p.AttemptTimeout;
            o.TotalRequestTimeout.Timeout = p.TotalTimeout;
        });

        return client;
    }
}