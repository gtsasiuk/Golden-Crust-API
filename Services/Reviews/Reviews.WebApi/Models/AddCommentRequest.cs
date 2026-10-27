namespace Reviews.WebApi.Models;

public record AddCommentRequest(long CustomerId, string AuthorName, string Text);