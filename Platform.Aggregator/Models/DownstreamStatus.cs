namespace Platform.Aggregator.Models;

public record DownstreamStatus(
    string Service, string Role, bool Reachable, int? HttpStatus, long ElapsedMs, string? Error);