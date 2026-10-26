namespace Catalog.Dal.Specifications;

public record MenuItemFilter(
	string? Search = null,
	long? CategoryId = null,
	long? TagId = null,
	decimal? MinPrice = null,
	decimal? MaxPrice = null,
	bool? IsAvailable = null,
	string SortBy = "name",
	bool Descending = false);