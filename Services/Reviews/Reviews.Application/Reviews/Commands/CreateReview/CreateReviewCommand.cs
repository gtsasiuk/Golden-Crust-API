using Reviews.Application.Common.Interfaces;
using Reviews.Domain.Enums;

namespace Reviews.Application.Reviews.Commands.CreateReview;

public record CreateReviewCommand(
    long MenuItemId,
    string MenuItemName,
    long CustomerId,
    string AuthorName,
    ReviewType Type,
    int Rating,
    string? Text = null,
    IReadOnlyList<string>? Photos = null,
    IReadOnlyList<string>? Pros = null,
    IReadOnlyList<string>? Cons = null,
    long? OrderId = null) : ICommand<string>;