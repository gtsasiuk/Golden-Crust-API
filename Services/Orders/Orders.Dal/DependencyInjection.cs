using Microsoft.Extensions.DependencyInjection;
using Orders.Dal.Interfaces;

namespace Orders.Dal;

public static class DalServiceCollectionExtensions
{
    // Scoped: один UnitOfWork (і одне з'єднання) на HTTP-запит. DI сам викличе DisposeAsync в кінці запиту.
    public static IServiceCollection AddOrdersDal(this IServiceCollection services, string connectionString)
        => services.AddScoped<IUnitOfWork>(_ => new UnitOfWork(connectionString));
}