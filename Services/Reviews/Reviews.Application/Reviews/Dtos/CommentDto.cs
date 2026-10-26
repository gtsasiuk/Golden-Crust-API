namespace Reviews.Application.Reviews.Dtos;

public sealed class CommentDto
{
    public long AuthorCustomerId { get; set; }
    public string AuthorName { get; set; } = "";
    public string Text { get; set; } = "";
    public DateTime CreatedAt { get; set; }
}