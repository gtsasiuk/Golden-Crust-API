using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using MongoDB.Bson;
using MongoDB.Driver;
using Reviews.Domain.Entities;
using Reviews.Infrastructure.Persistence;

namespace Reviews.Infrastructure.Seeding;

public class IndexCreationService(MongoDbContext context, ILogger<IndexCreationService> logger)
    : IHostedService
{
    public async Task StartAsync(CancellationToken ct)
    {
        var rk = Builders<Review>.IndexKeys;

        await context.Reviews.Indexes.CreateManyAsync(new[]
        {
            new CreateIndexModel<Review>(
                rk.Ascending(r => r.MenuItemId).Descending(r => r.CreatedAt),
                new CreateIndexOptions { Name = "ix_reviews_menuItem_createdAt" }),

            new CreateIndexModel<Review>(
                rk.Ascending(r => r.Author.CustomerId).Descending(r => r.CreatedAt),
                new CreateIndexOptions { Name = "ix_reviews_author_createdAt" }),

            new CreateIndexModel<Review>(
                rk.Ascending(r => r.OrderId).Ascending(r => r.MenuItemId),
                new CreateIndexOptions<Review>
                {
                    Name = "ux_reviews_order_menuItem",
                    Unique = true,
                    PartialFilterExpression = new BsonDocument("orderId", new BsonDocument("$exists", true))
                }),

            new CreateIndexModel<Review>(
                rk.Ascending(r => r.MenuItemId).Ascending(r => r.Author.CustomerId),
                new CreateIndexOptions { Name = "ux_reviews_menuItem_author", Unique = true }),

            new CreateIndexModel<Review>(
                rk.Text(r => r.Text),
                new CreateIndexOptions { Name = "ix_reviews_text", DefaultLanguage = "none" })
        }, ct);

        var dk = Builders<Discussion>.IndexKeys;
        await context.Discussions.Indexes.CreateOneAsync(
            new CreateIndexModel<Discussion>(
                dk.Ascending(d => d.MenuItemId).Descending(d => d.LastMessageAt),
                new CreateIndexOptions { Name = "ix_discussions_menuItem_lastMessage" }),
            cancellationToken: ct);

        logger.LogInformation("MongoDB indexes ensured");
    }

    public Task StopAsync(CancellationToken ct) => Task.CompletedTask;
}