namespace Platform.Aggregator.Models;

public record FullOrderDto(
	OrderInfo Order,
	IReadOnlyList<OrderLineDto> Items,
	IReadOnlyList<string> Warnings);

public record OrderInfo(
	long Id, long CustomerId, string Status, string? PaymentStatus,
	decimal TotalAmount, DateTime OrderDate, string DeliveryAddress);

public record OrderLineDto(
	long MenuItemId, string ProductName, decimal UnitPrice, int Quantity, decimal LineTotal,
	MenuInfo? Menu, ReviewsInfo? Reviews);

public record MenuInfo(
	string Name, string CategoryName, decimal CurrentPrice, bool IsAvailable,
	string? Description, string? ImageUrl, IReadOnlyList<string> Tags);

public record ReviewsInfo(long TotalCount, IReadOnlyList<ReviewInfo> Latest);

public record ReviewInfo(string Id, string AuthorName, int Rating, string? Text, string Type, DateTime CreatedAt);