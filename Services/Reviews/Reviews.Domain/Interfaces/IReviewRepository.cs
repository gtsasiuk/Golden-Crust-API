using MongoDB.Bson;
using Reviews.Domain.Entities;

namespace Reviews.Domain.Interfaces;

public interface IReviewRepository
{
    Task<Review?> GetByIdAsync(ObjectId id, CancellationToken ct = default);

    // "один відгук клієнта на страву" (для async-валідатора й unique-індексу)
    Task<bool> ExistsAsync(long menuItemId, long customerId, CancellationToken ct = default);

    Task AddAsync(Review review, CancellationToken ct = default);

    // застосовується лише до документа з очікуваною Version, інакше ConflictException
    Task UpdateAsync(Review review, CancellationToken ct = default);

    // true, якщо документ існував і був видалений
    Task<bool> DeleteAsync(ObjectId id, CancellationToken ct = default);
}