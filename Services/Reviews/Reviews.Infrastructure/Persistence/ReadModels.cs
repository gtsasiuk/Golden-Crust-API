using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;
using Reviews.Domain.Enums;

namespace Reviews.Infrastructure.Persistence;

[BsonIgnoreExtraElements]
internal sealed class AuthorReadModel
{
    public long CustomerId { get; set; }
    public string DisplayName { get; set; } = "";
}

[BsonIgnoreExtraElements]
internal sealed class CommentReadModel
{
    public AuthorReadModel Author { get; set; } = new();
    public string Text { get; set; } = "";
    public DateTime CreatedAt { get; set; }
}

[BsonIgnoreExtraElements]
internal sealed class ReviewReadModel
{
    public ObjectId Id { get; set; }
    public long MenuItemId { get; set; }
    public string MenuItemName { get; set; } = "";
    public long? OrderId { get; set; }
    public AuthorReadModel Author { get; set; } = new();

    [BsonSerializer(typeof(LowerCaseEnumSerializer<ReviewType>))]
    public ReviewType Type { get; set; }

    public int Rating { get; set; }
    public string? Text { get; set; }
    public List<string>? Photos { get; set; }
    public List<string>? Pros { get; set; }
    public List<string>? Cons { get; set; }
    public List<CommentReadModel>? Comments { get; set; }
    public ObjectId? DiscussionId { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
    public int Version { get; set; }
}