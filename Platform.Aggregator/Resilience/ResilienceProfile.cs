using Grpc.Core;
using Grpc.Net.Client.Configuration;

namespace Platform.Aggregator.Resilience;

public sealed record ResilienceProfile(
    string Name,
    string Address,
    string Role,
    int GrpcMaxAttempts,
    TimeSpan Backoff,
    TimeSpan MaxBackoff,
    double FailureRatio,
    int MinimumThroughput,
    TimeSpan BreakDuration,
    TimeSpan AttemptTimeout,
    TimeSpan CallDeadline)
{
    public TimeSpan SamplingDuration { get; init; } = TimeSpan.FromSeconds(30);

    public int HttpRetries => GrpcMaxAttempts - 1;

    public TimeSpan TotalTimeout => AttemptTimeout * (HttpRetries + 1) + MaxBackoff * HttpRetries;

    public ServiceConfig ToServiceConfig() => new()
    {
        MethodConfigs =
        {
            new MethodConfig
            {
                Names = { MethodName.Default },
                RetryPolicy = new RetryPolicy
                {
                    MaxAttempts = GrpcMaxAttempts,
                    InitialBackoff = Backoff,
                    MaxBackoff = MaxBackoff,
                    BackoffMultiplier = 2,
                    RetryableStatusCodes = { StatusCode.Unavailable }
                }
            }
        }
    };
}