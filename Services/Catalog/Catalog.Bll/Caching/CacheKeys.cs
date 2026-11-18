namespace Catalog.Bll.Caching;


public static class CacheKeys
{
    public const string Categories = "catalog:categories";

    public static string MenuItem(long id) => $"catalog:menu-item:{id}";
}