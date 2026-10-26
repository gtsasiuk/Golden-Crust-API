using MongoDB.Bson;
using Reviews.Domain.Common;
using Reviews.Domain.Enums;
using Reviews.Domain.Exceptions;
using Reviews.Domain.ValueObjects;

namespace Reviews.Domain.Entities;

// Заголовок обговорення; самі повідомлення лишаються окремою колекцією (їх може бути тисячі)
public class Discussion : BaseEntity
{
    public long MenuItemId { get; private set; }
    public ObjectId? ReviewId { get; private set; }
    public string Title { get; private set; } = "";
    public DiscussionStatus Status { get; private set; }
    public Author CreatedBy { get; private set; } = null!;
    public int MessageCount { get; private set; }
    public DateTime? LastMessageAt { get; private set; }

    private Discussion() { }

    public Discussion(long menuItemId, string title, Author createdBy, ObjectId? reviewId = null)
    {
        if (menuItemId <= 0)
            throw new DomainException("MenuItemId must be positive");
        if (string.IsNullOrWhiteSpace(title) || title.Trim().Length > 200)
            throw new DomainException("Title is required and must be at most 200 characters");

        MenuItemId = menuItemId;
        Title = title.Trim();
        CreatedBy = createdBy ?? throw new DomainException("Author is required");
        ReviewId = reviewId;
        Status = DiscussionStatus.Open;
    }

    public void RegisterMessage(DateTime at)
    {
        if (Status == DiscussionStatus.Closed)
            throw new DomainException("Discussion is closed");
        MessageCount++;
        LastMessageAt = at;
        Touch();
    }

    public void Close()
    {
        if (Status == DiscussionStatus.Closed)
            throw new DomainException("Discussion is already closed");
        Status = DiscussionStatus.Closed;
        Touch();
    }
}