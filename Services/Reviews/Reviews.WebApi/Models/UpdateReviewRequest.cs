namespace Reviews.WebApi.Models;

public record UpdateReviewRequest(
    int Rating,
    string? Text = null,
    IReadOnlyList<string>? Pros = null,
    IReadOnlyList<string>? Cons = null);