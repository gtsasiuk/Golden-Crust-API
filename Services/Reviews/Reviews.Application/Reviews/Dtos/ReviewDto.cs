using Reviews.Domain.Enums;

namespace Reviews.Application.Reviews.Dtos;

public sealed class ReviewDto
{
    public string Id { get; set; } = "";
    public long MenuItemId { get; set; }
    public string MenuItemName { get; set; } = "";
    public long? OrderId { get; set; }
    public long AuthorCustomerId { get; set; }
    public string AuthorName { get; set; } = "";
    public ReviewType Type { get; set; }
    public int Rating { get; set; }
    public string? Text { get; set; }
    public List<string> Photos { get; set; } = [];
    public List<string> Pros { get; set; } = [];
    public List<string> Cons { get; set; } = [];
    public List<CommentDto> Comments { get; set; } = [];
    public int CommentsCount { get; set; }
    public string? DiscussionId { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
    public int Version { get; set; }
}