using System.ComponentModel.DataAnnotations;
using Catalog.Domain.Entities;

namespace Catalog.Bll.Dto;

public record CategoryDto
{
	public long Id { get; init; }
	public string Name { get; init; } = "";
	public int SortOrder { get; init; }
}

public record CategoryWithItemsDto : CategoryDto
{
	public List<MenuItemListDto> Items { get; init; } = new();
}

public record MenuItemListDto
{
	public long Id { get; init; }
	public long CategoryId { get; init; }
	public string Name { get; init; } = "";
	public decimal Price { get; init; }
	public bool IsAvailable { get; init; }
	public string? PrimaryImageUrl { get; init; }
}

public record ImageDto
{
	public long Id { get; init; }
	public string Url { get; init; } = "";
	public string? AltText { get; init; }
	public bool IsPrimary { get; init; }
	public int SortOrder { get; init; }
}

public record TagDto
{
	public long Id { get; init; }
	public string Name { get; init; } = "";
}

public record MenuItemDto
{
	public long Id { get; init; }
	public long CategoryId { get; init; }
	public string CategoryName { get; init; } = "";
	public string Name { get; init; } = "";
	public decimal Price { get; init; }
	public bool IsAvailable { get; init; }
	public string? Description { get; init; }
	public string? Ingredients { get; init; }
	public int? WeightGrams { get; init; }
	public int? Calories { get; init; }
	public List<ImageDto> Images { get; init; } = new();
	public List<TagDto> Tags { get; init; } = new();
}

public record TagMenuItemDto
{
	public MenuItemListDto Item { get; init; } = null!;
	public DateTime AddedAt { get; init; }
}

public record CreateCategoryDto(
	[Required, StringLength(MenuCategory.NameMaxLength)] string Name,
	int SortOrder = 0);

public record UpdateCategoryDto(
	[Required, StringLength(MenuCategory.NameMaxLength)] string Name,
	int SortOrder = 0);

public record CreateMenuItemDto(
	long CategoryId,
	[Required, StringLength(MenuItem.NameMaxLength)] string Name,
	decimal Price);

public record UpdateMenuItemDto(
	long CategoryId,
	[Required, StringLength(MenuItem.NameMaxLength)] string Name,
	decimal Price);

public record ChangePriceDto(decimal Price);

public record SetAvailabilityDto(bool IsAvailable);

public record UpdateDetailsDto(
	[StringLength(MenuItemDetail.DescriptionMaxLength)] string? Description,
	[StringLength(MenuItemDetail.IngredientsMaxLength)] string? Ingredients,
	int? WeightGrams,
	int? Calories);

public record AddImageDto(
	[Required, StringLength(MenuItemImage.UrlMaxLength)] string Url,
	[StringLength(MenuItemImage.AltTextMaxLength)] string? AltText);

public record CreateTagDto(
	[Required, StringLength(Tag.NameMaxLength)] string Name);