using Reviews.Domain.Common;
using Reviews.Domain.Exceptions;

namespace Reviews.Domain.ValueObjects;

// Вбудований коментар у документі reviews (завжди читається разом із відгуком)
public sealed class Comment : ValueObject
{
    public const int MaxTextLength = 1000;

    public Author Author { get; }
    public string Text { get; }
    public DateTime CreatedAt { get; }

    public Comment(Author author, string text, DateTime createdAt)
    {
        if (author is null)
            throw new DomainException("Comment author is required");
        if (string.IsNullOrWhiteSpace(text))
            throw new DomainException("Comment text is required");
        if (text.Trim().Length > MaxTextLength)
            throw new DomainException($"Comment text must be at most {MaxTextLength} characters");

        Author = author;
        Text = text.Trim();
        CreatedAt = createdAt;
    }

    protected override IEnumerable<object?> GetEqualityComponents()
    {
        yield return Author;
        yield return Text;
        yield return CreatedAt;
    }
}