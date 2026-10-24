using Catalog.Domain.Common;

namespace Catalog.Domain.Entities;

public class Tag
{
    public const int NameMaxLength = 50;

    private readonly List<MenuItemTag> _itemTags = new();

    public long Id { get; private set; }
    public string Name { get; private set; } = "";
    public DateTime CreatedAt { get; private set; }

    public IReadOnlyCollection<MenuItemTag> ItemTags => _itemTags;

    private Tag() { }

    public Tag(string name) => Name = Guard.NotBlank(name, nameof(Name), NameMaxLength);

    public void Rename(string name) => Name = Guard.NotBlank(name, nameof(Name), NameMaxLength);
}