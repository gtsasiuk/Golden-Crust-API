using Grpc.Core;
using Grpc.Health.V1;
using Grpc.Net.ClientFactory;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace Platform.Aggregator.HealthChecks;

public abstract class GrpcServiceHealthCheck<TCheck> : IHealthCheck
    where TCheck : class
{
    private readonly Health.HealthClient _client;
    private readonly ILogger _logger;
    private readonly string _serviceName;

    protected GrpcServiceHealthCheck(
        GrpcClientFactory clientFactory,
        ILogger<TCheck> logger,
        string clientName,
        string serviceName)
    {
        _client = clientFactory.CreateClient<Health.HealthClient>(clientName);
        _logger = logger;
        _serviceName = serviceName;
    }

    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var response = await _client.CheckAsync(
                new HealthCheckRequest(),
                deadline: DateTime.UtcNow.AddSeconds(2),
                cancellationToken: cancellationToken);

            if (response.Status ==
                HealthCheckResponse.Types.ServingStatus.Serving)
            {
                return HealthCheckResult.Healthy(
                    $"{_serviceName} is serving");
            }

            return HealthCheckResult.Unhealthy(
                $"{_serviceName} status: {response.Status}");
        }
        catch (RpcException ex)
        {
            _logger.LogWarning(
                ex,
                "gRPC health check failed for {ServiceName}",
                _serviceName);

            return HealthCheckResult.Unhealthy(
                $"{_serviceName} is unavailable",
                ex);
        }
    }
}
