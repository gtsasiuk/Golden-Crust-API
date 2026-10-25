using Catalog.Dal.Interfaces;
using Catalog.Dal.Repositories;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.DependencyInjection;

namespace Catalog.Dal;

public static class DependencyInjection
{
    public static IServiceCollection AddCatalogDal(this IServiceCollection services, string connectionString)
    {
        services.AddDbContext<CatalogDbContext>(o => o
            .UseSqlServer(connectionString)

            .ConfigureWarnings(w => w.Ignore(
                CoreEventId.PossibleIncorrectRequiredNavigationWithQueryFilterInteractionWarning)));

        services.AddScoped<IUnitOfWork, UnitOfWork>();

        return services;
    }
}