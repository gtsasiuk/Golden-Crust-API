using Grpc.Net.ClientFactory;

namespace Platform.Aggregator.HealthChecks;

public sealed class OrdersGrpcHealthCheck
    : GrpcServiceHealthCheck<OrdersGrpcHealthCheck>
{
    public OrdersGrpcHealthCheck(
        GrpcClientFactory clientFactory,
        ILogger<OrdersGrpcHealthCheck> logger)
        : base(
            clientFactory,
            logger,
            "orders-health",
            "orders-api")
    {
    }
}