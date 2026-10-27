using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Reviews.Application.Common.Interfaces;
using Reviews.Domain.Interfaces;
using Reviews.Infrastructure.Persistence;
using Reviews.Infrastructure.Seeding;

namespace Reviews.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(
        this IServiceCollection services, IConfiguration config)
    {
        MongoMappings.Register();

        services.Configure<MongoDbOptions>(o =>
        {
            config.GetSection(MongoDbOptions.SectionName).Bind(o);

            var fromAspire = config.GetConnectionString("ReviewsDb");
            if (!string.IsNullOrWhiteSpace(fromAspire))
                o.ConnectionString = fromAspire;
        });
        services.AddSingleton<MongoDbContext>();

        services.AddScoped<IReviewRepository, ReviewRepository>();
        services.AddScoped<IReviewQueries, ReviewQueries>();

        services.AddSingleton<IDataSeeder, ReviewSeeder>();

        services.AddHostedService<IndexCreationService>();
        services.AddHostedService<DataSeedingService>();

        return services;
    }
}