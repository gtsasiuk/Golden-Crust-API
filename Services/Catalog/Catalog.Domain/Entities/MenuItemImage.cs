using Catalog.Domain.Common;

namespace Catalog.Domain.Entities;

public class MenuItemImage
{
    public const int UrlMaxLength = 500;
    public const int AltTextMaxLength = 200;

    public long Id { get; private set; }
    public long MenuItemId { get; private set; }
    public string Url { get; private set; } = "";
    public string? AltText { get; private set; }
    public bool IsPrimary { get; private set; }
    public int SortOrder { get; private set; }
    public DateTime CreatedAt { get; private set; }

    private MenuItemImage() { }

    internal MenuItemImage(string url, string? altText, bool isPrimary, int sortOrder)
    {
        Url = Guard.NotBlank(url, nameof(Url), UrlMaxLength);
        AltText = Guard.OptionalMax(altText, nameof(AltText), AltTextMaxLength);
        IsPrimary = isPrimary;
        SortOrder = sortOrder;
    }

    internal void SetPrimary(bool isPrimary) => IsPrimary = isPrimary;
}