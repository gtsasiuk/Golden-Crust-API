using System.Diagnostics;
using Platform.Aggregator.Models;
using Platform.Aggregator.Resilience;

namespace Platform.Aggregator.Services;

public class DownstreamStatusService(IHttpClientFactory factory, ILogger<DownstreamStatusService> logger)
{
    public async Task<IReadOnlyList<DownstreamStatus>> CheckAllAsync(CancellationToken ct)
        => await Task.WhenAll(ResilienceProfiles.All.Select(p => CheckAsync(p, ct)));

    private async Task<DownstreamStatus> CheckAsync(ResilienceProfile p, CancellationToken ct)
    {
        var sw = Stopwatch.StartNew();
        try
        {
            using var response = await factory.CreateClient(p.Name).GetAsync("/health/live", ct);
            return new(p.Name, p.Role, response.IsSuccessStatusCode, (int)response.StatusCode, sw.ElapsedMilliseconds, null);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "{Service} status check failed after {ElapsedMs} ms", p.Name, sw.ElapsedMilliseconds);
            return new(p.Name, p.Role, false, null, sw.ElapsedMilliseconds, ex.GetType().Name);
        }
    }
}