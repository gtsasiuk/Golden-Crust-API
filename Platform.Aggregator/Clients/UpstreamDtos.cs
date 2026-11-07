namespace Platform.Aggregator.Clients;

public record OrderDto(
	long Id, long CustomerId, string Status, decimal TotalAmount, DateTime OrderDate,
	string DeliveryAddress, string? PaymentStatus, List<OrderItemDto> Items);

public record OrderItemDto(long MenuItemId, string ProductName, decimal UnitPrice, int Quantity, decimal LineTotal);

public record CatalogItemDto(
	long Id, string Name, string CategoryName, decimal Price, bool IsAvailable,
	string? Description, List<CatalogTagDto> Tags, List<CatalogImageDto> Images);

public record CatalogTagDto(string Name);
public record CatalogImageDto(string Url, bool IsPrimary);

public record ReviewsPageDto(List<ReviewItemDto> Items, long TotalCount);

public record ReviewItemDto(
	string Id, long MenuItemId, string AuthorName, string Type, int Rating, string? Text, DateTime CreatedAt);