namespace Catalog.Bll.Dto;

public record MenuItemsQuery(
    int Page = 1,
    int PageSize = 20,
    string? Search = null,
    long? CategoryId = null,
    long? TagId = null,
    decimal? MinPrice = null,
    decimal? MaxPrice = null,
    bool? IsAvailable = null,
    string SortBy = "name",
    string SortDir = "asc");