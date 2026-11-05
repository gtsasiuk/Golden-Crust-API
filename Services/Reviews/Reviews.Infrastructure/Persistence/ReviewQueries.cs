using MongoDB.Bson;
using MongoDB.Driver;
using Reviews.Application.Common.Interfaces;
using Reviews.Application.Common.Models;
using Reviews.Application.Reviews.Dtos;

namespace Reviews.Infrastructure.Persistence;

public class ReviewQueries(MongoDbContext context) : IReviewQueries
{
    private static readonly ProjectionDefinition<ReviewReadModel> DtoFields =
        Builders<ReviewReadModel>.Projection
            .Include(r => r.MenuItemId).Include(r => r.MenuItemName).Include(r => r.OrderId)
            .Include(r => r.Author).Include(r => r.Type).Include(r => r.Rating).Include(r => r.Text)
            .Include(r => r.Photos).Include(r => r.Pros).Include(r => r.Cons).Include(r => r.Comments)
            .Include(r => r.DiscussionId).Include(r => r.CreatedAt).Include(r => r.UpdatedAt)
            .Include(r => r.Version);

    private IMongoCollection<ReviewReadModel> Reviews => context.ReviewReadModels;

    public async Task<ReviewDto?> GetByIdAsync(string id, CancellationToken ct)
    {
        if (!ObjectId.TryParse(id, out var objectId))
            return null;

        var model = await Reviews.Find(r => r.Id == objectId)
            .Project<ReviewReadModel>(DtoFields)
            .FirstOrDefaultAsync(ct);

        return model is null ? null : ToDto(model, includeComments: true);
    }

    public async Task<PagedResult<ReviewDto>> GetListAsync(
        long? menuItemId, int page, int pageSize, CancellationToken ct)
    {
        var filter = menuItemId is { } id
            ? Builders<ReviewReadModel>.Filter.Eq(r => r.MenuItemId, id)
            : Builders<ReviewReadModel>.Filter.Empty;

        var total = await Reviews.CountDocumentsAsync(filter, cancellationToken: ct);

        var models = await Reviews.Find(filter)
            .SortByDescending(r => r.CreatedAt).ThenByDescending(r => r.Id)
            .Skip((page - 1) * pageSize)
            .Limit(pageSize)
            .Project<ReviewReadModel>(DtoFields)
            .ToListAsync(ct);

        return new PagedResult<ReviewDto>
        {
            Items = models.Select(m => ToDto(m, includeComments: false)).ToList(),
            Page = page,
            PageSize = pageSize,
            TotalCount = total
        };
    }

    private static ReviewDto ToDto(ReviewReadModel m, bool includeComments) => new()
    {
        Id = m.Id.ToString(),
        MenuItemId = m.MenuItemId,
        MenuItemName = m.MenuItemName,
        OrderId = m.OrderId,
        AuthorCustomerId = m.Author.CustomerId,
        AuthorName = m.Author.DisplayName,
        Type = m.Type,
        Rating = m.Rating,
        Text = m.Text,
        Photos = m.Photos ?? new(),
        Pros = m.Pros ?? new(),
        Cons = m.Cons ?? new(),
        Comments = includeComments && m.Comments is not null
            ? m.Comments.Select(c => new CommentDto
            {
                AuthorCustomerId = c.Author.CustomerId,
                AuthorName = c.Author.DisplayName,
                Text = c.Text,
                CreatedAt = c.CreatedAt
            }).ToList()
            : new(),
        CommentsCount = m.Comments?.Count ?? 0,
        DiscussionId = m.DiscussionId?.ToString(),
        CreatedAt = m.CreatedAt,
        UpdatedAt = m.UpdatedAt,
        Version = m.Version
    };
}