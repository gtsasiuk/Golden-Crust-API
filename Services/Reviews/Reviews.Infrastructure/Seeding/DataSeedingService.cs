using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Reviews.Infrastructure.Seeding;

public class DataSeedingService(IEnumerable<IDataSeeder> seeders, ILogger<DataSeedingService> logger)
    : IHostedService
{
    public async Task StartAsync(CancellationToken ct)
    {
        foreach (var seeder in seeders)
        {
            logger.LogInformation("Running seeder {Seeder}", seeder.GetType().Name);
            await seeder.SeedAsync(ct);
        }
    }

    public Task StopAsync(CancellationToken ct) => Task.CompletedTask;
}