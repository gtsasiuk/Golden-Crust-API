namespace Catalog.Domain.Entities;

public class MenuItemTag
{
    public long MenuItemId { get; private set; }
    public MenuItem MenuItem { get; private set; } = null!;

    public long TagId { get; private set; }
    public Tag Tag { get; private set; } = null!;

    public DateTime AddedAt { get; private set; }

    private MenuItemTag() { }

    internal MenuItemTag(long tagId) => TagId = tagId;
}