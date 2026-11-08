using MediatR;
using Reviews.Application.Caching;
using Reviews.Application.Common.Interfaces;
using Reviews.Application.Common.Models;
using Reviews.Application.Reviews.Dtos;

namespace Reviews.Application.Reviews.Queries.GetReviewsList;

public class GetReviewsListQueryHandler(IReviewQueries queries, ICacheService cache)
    : IRequestHandler<GetReviewsListQuery, PagedResult<ReviewDto>>
{
    public async Task<PagedResult<ReviewDto>> Handle(GetReviewsListQuery q, CancellationToken ct)
    {
        var version = await cache.GetListVersionAsync(ct);
        var key = CacheKeys.List(version, q.MenuItemId, q.Page, q.PageSize);

        var result = await cache.GetOrCreateAsync(key,
            async () => await queries.GetListAsync(q.MenuItemId, q.Page, q.PageSize, ct),
            CachePolicy.ReviewsList, ct);
        return result!;
    }
}