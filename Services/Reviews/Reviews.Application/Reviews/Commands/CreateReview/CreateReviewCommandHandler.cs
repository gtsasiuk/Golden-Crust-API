using System.Diagnostics;
using MediatR;
using Microsoft.Extensions.Logging;
using Reviews.Application.Caching;
using Reviews.Domain.Entities;
using Reviews.Domain.Interfaces;
using Reviews.Domain.ValueObjects;

namespace Reviews.Application.Reviews.Commands.CreateReview;

public class CreateReviewCommandHandler(
    IReviewRepository reviews, ICacheService cache, ILogger<CreateReviewCommandHandler> logger)
    : IRequestHandler<CreateReviewCommand, string>
{
    private static readonly ActivitySource Source = new("GoldenCrust.Reviews");

    public async Task<string> Handle(CreateReviewCommand cmd, CancellationToken ct)
    {
        using var activity = Source.StartActivity("CreateReview");
        activity?.SetTag("menuitem.id", cmd.MenuItemId);

        var review = new Review(
            cmd.MenuItemId, cmd.MenuItemName,
            new Author(cmd.CustomerId, cmd.AuthorName),
            cmd.Type, cmd.Rating, cmd.Text,
            cmd.Photos, cmd.Pros, cmd.Cons, cmd.OrderId);

        await reviews.AddAsync(review, ct);
        await cache.RemoveAsync(CacheKeys.ListVersion, "ReviewCreated", ct);

        activity?.SetTag("review.id", review.Id.ToString());
        logger.LogInformation(
            "Review {ReviewId} created for menu item {MenuItemId} by customer {CustomerId}, rating {Rating}, type {ReviewType}",
            review.Id, cmd.MenuItemId, cmd.CustomerId, cmd.Rating, cmd.Type);

        return review.Id.ToString();
    }
}