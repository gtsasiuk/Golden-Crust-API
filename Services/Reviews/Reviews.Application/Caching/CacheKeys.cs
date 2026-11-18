namespace Reviews.Application.Caching;

public static class CacheKeys
{
    public const string ListVersion = "reviews:list-version";

    public static string List(string version, long? menuItemId, int page, int pageSize)
        => $"reviews:list:v{version}:item:{(menuItemId?.ToString() ?? "all")}:page:{page}:size:{pageSize}";
}