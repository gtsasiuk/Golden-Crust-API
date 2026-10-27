using MongoDB.Bson;
using Reviews.Domain.Common;
using Reviews.Domain.Enums;
using Reviews.Domain.Exceptions;
using Reviews.Domain.ValueObjects;

namespace Reviews.Domain.Entities;

public class Review : BaseEntity
{
    public const int MaxTextLength = 2000;
    public const int MaxPhotos = 5;

    // вкладені масиви документа; назовні лише IReadOnlyCollection
    private List<string> _photos = new();
    private List<string> _pros = new();
    private List<string> _cons = new();
    private List<Comment> _comments = new();

    public long MenuItemId { get; private set; }               // посилання на Catalog (без FK)
    public string MenuItemName { get; private set; } = "";     // денормалізовано з Catalog
    public long? OrderId { get; private set; }                 // посилання на Orders (перевірений відгук)
    public Author Author { get; private set; } = null!;        // Value Object
    public ReviewType Type { get; private set; }
    public int Rating { get; private set; }
    public string? Text { get; private set; }
    public ObjectId? DiscussionId { get; private set; }        // reference на discussions

    public IReadOnlyCollection<string> Photos => _photos.AsReadOnly();
    public IReadOnlyCollection<string> Pros => _pros.AsReadOnly();
    public IReadOnlyCollection<string> Cons => _cons.AsReadOnly();
    public IReadOnlyCollection<Comment> Comments => _comments.AsReadOnly();

    private Review() { } // для десеріалізації MongoDB

    public Review(
        long menuItemId, string menuItemName, Author author,
        ReviewType type, int rating, string? text = null,
        IEnumerable<string>? photos = null,
        IEnumerable<string>? pros = null,
        IEnumerable<string>? cons = null,
        long? orderId = null)
    {
        if (menuItemId <= 0)
            throw new DomainException("MenuItemId must be positive");
        if (string.IsNullOrWhiteSpace(menuItemName))
            throw new DomainException("MenuItemName is required");
        if (orderId is <= 0)
            throw new DomainException("OrderId must be positive");

        var normalizedText = NormalizeText(text);
        var photoList = Clean(photos);
        var prosList = Clean(pros);
        var consList = Clean(cons);

        ValidateContent(type, rating, normalizedText, photoList, prosList, consList);

        MenuItemId = menuItemId;
        MenuItemName = menuItemName.Trim();
        OrderId = orderId;
        Author = author ?? throw new DomainException("Author is required");
        Type = type;
        Rating = rating;
        Text = normalizedText;
        _photos.AddRange(photoList);
        _pros.AddRange(prosList);
        _cons.AddRange(consList);
    }

    // Редагування: тип відгуку й фото не змінюються, інваріанти ті самі, що й при створенні
    public void Edit(int rating, string? text, IEnumerable<string>? pros, IEnumerable<string>? cons)
    {
        var normalizedText = NormalizeText(text);
        var prosList = Clean(pros);
        var consList = Clean(cons);

        ValidateContent(Type, rating, normalizedText, _photos, prosList, consList);

        Rating = rating;
        Text = normalizedText;
        _pros.Clear();
        _pros.AddRange(prosList);
        _cons.Clear();
        _cons.AddRange(consList);
        Touch();
    }

    public void AddComment(Author author, string text)
    {
        _comments.Add(new Comment(author, text, DateTime.UtcNow)); // валідація всередині Comment
        Touch();
    }

    public void AttachDiscussion(ObjectId discussionId)
    {
        DiscussionId = discussionId;
        Touch();
    }

    // Правила збігаються з JSON Schema з Лаби 1 (collections.js): Mongo не відхилить те, що пропустив домен
    private static void ValidateContent(
        ReviewType type, int rating, string? text,
        IReadOnlyCollection<string> photos,
        IReadOnlyCollection<string> pros,
        IReadOnlyCollection<string> cons)
    {
        if (!Enum.IsDefined(type))
            throw new DomainException("Unknown review type");
        if (rating is < 1 or > 5)
            throw new DomainException("Rating must be between 1 and 5");
        if (text is { Length: > MaxTextLength })
            throw new DomainException($"Text must be at most {MaxTextLength} characters");
        if (photos.Count > MaxPhotos)
            throw new DomainException($"A review can have at most {MaxPhotos} photos");

        switch (type)
        {
            case ReviewType.Quick:
                if (text is not null || photos.Count > 0 || pros.Count > 0 || cons.Count > 0)
                    throw new DomainException("A quick review contains only a rating");
                break;
            case ReviewType.Text:
                if (text is null)
                    throw new DomainException("A text review requires text");
                if (photos.Count > 0)
                    throw new DomainException("Photos are allowed only in a photo review");
                break;
            case ReviewType.Photo:
                if (photos.Count == 0)
                    throw new DomainException("A photo review requires at least one photo");
                break;
        }
    }

    private static string? NormalizeText(string? text)
        => string.IsNullOrWhiteSpace(text) ? null : text.Trim();

    private static List<string> Clean(IEnumerable<string>? items)
        => items?.Where(s => !string.IsNullOrWhiteSpace(s)).Select(s => s.Trim()).ToList() ?? new();
}