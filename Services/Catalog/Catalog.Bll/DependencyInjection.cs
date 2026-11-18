using Catalog.Bll.Services;
using Catalog.Bll.Caching;
using Catalog.Bll.Validators;
using FluentValidation;
using Microsoft.Extensions.DependencyInjection;

namespace Catalog.Bll;

public static class DependencyInjection
{
    public static IServiceCollection AddCatalogBll(this IServiceCollection services)
    {
        services.AddScoped<ICategoryService, CategoryService>();
        services.AddScoped<IMenuItemService, MenuItemService>();
        services.AddScoped<ITagService, TagService>();
        services.AddSingleton<ICacheService, CacheService>();
        services.AddValidatorsFromAssemblyContaining<CreateMenuItemDtoValidator>();
        return services;
    }
}