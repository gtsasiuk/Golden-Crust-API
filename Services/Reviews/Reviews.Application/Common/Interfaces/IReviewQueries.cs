using Reviews.Application.Common.Models;
using Reviews.Application.Reviews.Dtos;

namespace Reviews.Application.Common.Interfaces;

public interface IReviewQueries
{
    Task<ReviewDto?> GetByIdAsync(string id, CancellationToken ct);

    Task<PagedResult<ReviewDto>> GetListAsync(
        long? menuItemId, int page, int pageSize, CancellationToken ct);
}