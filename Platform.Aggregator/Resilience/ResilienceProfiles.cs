namespace Platform.Aggregator.Resilience;

public static class ResilienceProfiles
{
    public static readonly ResilienceProfile Orders = new(
        Name: "orders-api", Address: "https://orders-api", Role: "critical",
        GrpcMaxAttempts: 4, Backoff: TimeSpan.FromMilliseconds(200), MaxBackoff: TimeSpan.FromSeconds(2),
        FailureRatio: 0.3, MinimumThroughput: 5, BreakDuration: TimeSpan.FromSeconds(10),
        AttemptTimeout: TimeSpan.FromSeconds(3), CallDeadline: TimeSpan.FromSeconds(10));

    public static readonly ResilienceProfile Catalog = new(
        Name: "catalog-api", Address: "https://catalog-api", Role: "critical",
        GrpcMaxAttempts: 3, Backoff: TimeSpan.FromMilliseconds(200), MaxBackoff: TimeSpan.FromSeconds(2),
        FailureRatio: 0.4, MinimumThroughput: 5, BreakDuration: TimeSpan.FromSeconds(10),
        AttemptTimeout: TimeSpan.FromSeconds(2), CallDeadline: TimeSpan.FromSeconds(6));

    public static readonly ResilienceProfile Reviews = new(
        Name: "reviews-api", Address: "https://reviews-api", Role: "non-critical",
        GrpcMaxAttempts: 2, Backoff: TimeSpan.FromMilliseconds(100), MaxBackoff: TimeSpan.FromMilliseconds(500),
        FailureRatio: 0.7, MinimumThroughput: 5, BreakDuration: TimeSpan.FromSeconds(20),
        AttemptTimeout: TimeSpan.FromSeconds(1), CallDeadline: TimeSpan.FromSeconds(2));

    public static IReadOnlyList<ResilienceProfile> All { get; } = [Orders, Catalog, Reviews];
}