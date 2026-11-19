using Grpc.Net.ClientFactory;

namespace Platform.Aggregator.HealthChecks;

public sealed class ReviewsGrpcHealthCheck
    : GrpcServiceHealthCheck<ReviewsGrpcHealthCheck>
{
    public ReviewsGrpcHealthCheck(
        GrpcClientFactory clientFactory,
        ILogger<ReviewsGrpcHealthCheck> logger)
        : base(
            clientFactory,
            logger,
            "reviews-health",
            "reviews-api")
    {
    }
}