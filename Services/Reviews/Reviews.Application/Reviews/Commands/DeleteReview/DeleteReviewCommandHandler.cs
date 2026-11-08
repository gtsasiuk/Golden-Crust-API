using MediatR;
using MongoDB.Bson;
using Reviews.Application.Caching;
using Reviews.Domain.Entities;
using Reviews.Domain.Exceptions;
using Reviews.Domain.Interfaces;

namespace Reviews.Application.Reviews.Commands.DeleteReview;

public class DeleteReviewCommandHandler(IReviewRepository reviews, ICacheService cache)
    : IRequestHandler<DeleteReviewCommand>
{
    public async Task Handle(DeleteReviewCommand cmd, CancellationToken ct)
    {
        var deleted = await reviews.DeleteAsync(ObjectId.Parse(cmd.Id), ct);
        if (!deleted)
            throw new NotFoundException(nameof(Review), cmd.Id);

        await cache.RemoveAsync(CacheKeys.ListVersion, "ReviewDeleted", ct);
    }
}