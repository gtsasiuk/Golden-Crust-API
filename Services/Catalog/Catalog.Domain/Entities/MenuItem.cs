using Catalog.Domain.Common;
using Catalog.Domain.Exceptions;

namespace Catalog.Domain.Entities;

public class MenuItem : AuditableEntity
{
    public const int NameMaxLength = 200;
    public const decimal MaxPrice = 100_000m;

    private readonly List<MenuItemImage> _images = new();
    private readonly List<MenuItemTag> _itemTags = new();

    public long CategoryId { get; private set; }
    public MenuCategory Category { get; private set; } = null!;
    public string Name { get; private set; } = "";
    public decimal Price { get; private set; }
    public bool IsAvailable { get; private set; } = true;

    public MenuItemDetail? Detail { get; private set; }                       
    public IReadOnlyCollection<MenuItemImage> Images => _images;              
    public IReadOnlyCollection<MenuItemTag> ItemTags => _itemTags;            

    private MenuItem() { }

    public MenuItem(long categoryId, string name, decimal price)
    {
        if (categoryId <= 0) throw new DomainException("CategoryId is required");
        CategoryId = categoryId;
        Name = Guard.NotBlank(name, nameof(Name), NameMaxLength);
        Price = Guard.Price(price, MaxPrice);
    }

    public void Rename(string name) => Name = Guard.NotBlank(name, nameof(Name), NameMaxLength);

    public void ChangePrice(decimal price) => Price = Guard.Price(price, MaxPrice);

    public void SetAvailability(bool isAvailable) => IsAvailable = isAvailable;

    public void MoveToCategory(long categoryId)
    {
        if (categoryId <= 0) throw new DomainException("CategoryId is required");
        CategoryId = categoryId;
    }

    public void SetDetail(string? description, string? ingredients, int? weightGrams, int? calories)
    {
        if (Detail is null)
            Detail = new MenuItemDetail(description, ingredients, weightGrams, calories);
        else
            Detail.Update(description, ingredients, weightGrams, calories);
    }

    public MenuItemImage AddImage(string url, string? altText)
    {
        var sortOrder = _images.Count == 0 ? 0 : _images.Max(i => i.SortOrder) + 1;
        var image = new MenuItemImage(url, altText, isPrimary: _images.Count == 0, sortOrder);
        _images.Add(image);
        return image;
    }

    public void UnsetPrimaryImages()
    {
        foreach (var image in _images.Where(i => i.IsPrimary))
            image.SetPrimary(false);
    }

    public void SetPrimaryImage(long imageId)
    {
        var target = _images.FirstOrDefault(i => i.Id == imageId)
                     ?? throw new EntityNotFoundException(nameof(MenuItemImage), imageId);
        target.SetPrimary(true);
    }

    public void RemoveImage(long imageId)
    {
        var image = _images.FirstOrDefault(i => i.Id == imageId)
                    ?? throw new EntityNotFoundException(nameof(MenuItemImage), imageId);

        if (image.IsPrimary && _images.Count > 1)
            throw new DomainException("Assign another primary image before removing the primary one");

        _images.Remove(image);
    }

    public void AddTag(long tagId)
    {
        if (_itemTags.Any(t => t.TagId == tagId))
            throw new DomainException("Tag is already assigned to this menu item");

        _itemTags.Add(new MenuItemTag(tagId));
    }

    public void RemoveTag(long tagId)
    {
        var link = _itemTags.FirstOrDefault(t => t.TagId == tagId)
                   ?? throw new EntityNotFoundException(nameof(MenuItemTag), tagId);
        _itemTags.Remove(link);
    }
}