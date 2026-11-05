using MediatR;
using Reviews.Domain.Entities;
using Reviews.Domain.Interfaces;
using Reviews.Domain.ValueObjects;

namespace Reviews.Application.Reviews.Commands.CreateReview;

public class CreateReviewCommandHandler(IReviewRepository reviews)
    : IRequestHandler<CreateReviewCommand, string>
{
    public async Task<string> Handle(CreateReviewCommand cmd, CancellationToken ct)
    {
        var review = new Review(
            cmd.MenuItemId, cmd.MenuItemName,
            new Author(cmd.CustomerId, cmd.AuthorName),
            cmd.Type, cmd.Rating, cmd.Text,
            cmd.Photos, cmd.Pros, cmd.Cons, cmd.OrderId);

        await reviews.AddAsync(review, ct);
        return review.Id.ToString();
    }
}