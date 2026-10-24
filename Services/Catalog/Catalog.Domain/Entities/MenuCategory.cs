using Catalog.Domain.Common;

namespace Catalog.Domain.Entities;

public class MenuCategory : AuditableEntity
{
    public const int NameMaxLength = 100;

    private readonly List<MenuItem> _items = new();

    public string Name { get; private set; } = "";
    public int SortOrder { get; private set; }

    public IReadOnlyCollection<MenuItem> Items => _items;

    private MenuCategory() { }

    public MenuCategory(string name, int sortOrder = 0)
    {
        Name = Guard.NotBlank(name, nameof(Name), NameMaxLength);
        SortOrder = sortOrder;
    }

    public void Rename(string name) => Name = Guard.NotBlank(name, nameof(Name), NameMaxLength);
    public void ChangeSortOrder(int sortOrder) => SortOrder = sortOrder;
}