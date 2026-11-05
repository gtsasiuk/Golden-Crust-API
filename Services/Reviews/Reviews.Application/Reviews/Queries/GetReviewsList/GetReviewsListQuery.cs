using Reviews.Application.Common.Interfaces;
using Reviews.Application.Common.Models;
using Reviews.Application.Reviews.Dtos;

namespace Reviews.Application.Reviews.Queries.GetReviewsList;

public record GetReviewsListQuery(long? MenuItemId, int Page = 1, int PageSize = 20)
    : IQuery<PagedResult<ReviewDto>>;