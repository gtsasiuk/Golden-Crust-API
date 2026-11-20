using Microsoft.Extensions.Logging;
using MongoDB.Bson;
using MongoDB.Driver;
using Platform.EventBus.Contracts;
using Reviews.Domain.Entities;
using Reviews.Infrastructure.Persistence;

namespace Reviews.Infrastructure.IntegrationEvents;

public sealed class ProductRenamedHandler(
    MongoDbContext context,
    ILogger<ProductRenamedHandler> logger)
{
    private readonly IMongoCollection<Review> _reviews = context.Reviews;
    private readonly IMongoCollection<BsonDocument> _processedEvents =
        context.ProcessedEvents;

    public async Task HandleAsync(
        MenuItemRenamedEvent e,
        CancellationToken cancellationToken = default)
    {
        var eventId = e.EventId.ToString("D");

        if (e.MenuItemId <= 0 || string.IsNullOrWhiteSpace(e.NewName))
        {
            throw new ArgumentException(
                "Invalid MenuItemRenamedEvent payload");
        }

        // Перевіряємо, чи подія вже була успішно оброблена.
        var alreadyProcessed = await _processedEvents
            .Find(Builders<BsonDocument>.Filter.Eq("_id", eventId))
            .AnyAsync(cancellationToken);

        if (alreadyProcessed)
        {
            logger.LogInformation(
                "Event {EventId} already processed. Skipping duplicate",
                eventId);

            return;
        }

        // Оновлюємо назву в усіх відгуках відповідного товару.
        var filter = Builders<Review>.Filter
            .Eq(r => r.MenuItemId, e.MenuItemId);

        var update = Builders<Review>.Update
            .Set("menuItemName", e.NewName.Trim())
            .Set("updatedAt", DateTime.UtcNow)
            .Inc("version", 1);

        var result = await _reviews.UpdateManyAsync(
            filter,
            update,
            cancellationToken: cancellationToken);

        // Реєструємо EventId тільки після успішного оновлення відгуків.
        // Повторна доставка безпечна: присвоєння тієї самої назви
        // не змінює бізнес-результат.
        var processedEvent = new BsonDocument
        {
            { "_id", eventId },
            { "eventType", nameof(MenuItemRenamedEvent) },
            { "menuItemId", e.MenuItemId },
            { "newName", e.NewName.Trim() },
            { "processedAt", DateTime.UtcNow }
        };

        try
        {
            await _processedEvents.InsertOneAsync(
                processedEvent,
                cancellationToken: cancellationToken);
        }
        catch (MongoWriteException ex)
            when (ex.WriteError?.Category == ServerErrorCategory.DuplicateKey)
        {
            // Інший екземпляр Consumer уже зареєстрував цю подію.
            logger.LogInformation(
                "Event {EventId} was registered by another consumer",
                eventId);
        }

        logger.LogInformation(
            "Product renamed event processed. EventId: {EventId}, " +
            "MenuItemId: {MenuItemId}, NewName: {NewName}, " +
            "MatchedReviews: {MatchedCount}, ModifiedReviews: {ModifiedCount}",
            eventId,
            e.MenuItemId,
            e.NewName,
            result.MatchedCount,
            result.ModifiedCount);
    }
}
