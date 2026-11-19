using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Platform.ServiceDefaults;

public abstract class PeriodicRefreshService(ILogger logger, TimeSpan interval) : BackgroundService
{
    protected abstract Task RefreshAsync(CancellationToken ct);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(interval);
        try
        {
            do
            {
                try { await RefreshAsync(stoppingToken); }
                catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { throw; }
                catch (Exception ex)
                {
                    logger.LogWarning(ex, "Metrics snapshot refresh failed, keeping previous values");
                }
            }
            while (await timer.WaitForNextTickAsync(stoppingToken));
        }
        catch (OperationCanceledException) { }
    }
}