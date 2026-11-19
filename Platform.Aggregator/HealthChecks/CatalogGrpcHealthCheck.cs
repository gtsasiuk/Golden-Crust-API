using Grpc.Net.ClientFactory;

namespace Platform.Aggregator.HealthChecks;

public sealed class CatalogGrpcHealthCheck
    : GrpcServiceHealthCheck<CatalogGrpcHealthCheck>
{
    public CatalogGrpcHealthCheck(
        GrpcClientFactory clientFactory,
        ILogger<CatalogGrpcHealthCheck> logger)
        : base(
            clientFactory,
            logger,
            "catalog-health",
            "catalog-api")
    {
    }
}