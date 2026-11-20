
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
        // 1. Перевіряємо коректність події.
        if (e.EventId == Guid.Empty)
        {
            throw new ArgumentException(
                "EventId cannot be empty");
        }

        if (e.MenuItemId <= 0 || string.IsNullOrWhiteSpace(e.NewName))
        {
            throw new ArgumentException(
                "Invalid MenuItemRenamedEvent payload");
        }

        var eventId = e.EventId.ToString("D");
        var newName = e.NewName.Trim();

        // Зберігаємо час події з точністю до 100 наносекунд.
        // Це дозволяє правильно порівнювати послідовність подій.
        var occurredAt = e.OccurredAt.Kind == DateTimeKind.Unspecified
            ? DateTime.SpecifyKind(e.OccurredAt, DateTimeKind.Utc)
            : e.OccurredAt.ToUniversalTime();

        var occurredAtTicks = occurredAt.Ticks;

        // 2. Перевіряємо, чи подія вже була оброблена.
        // Ця перевірка є оптимізацією.
        // Основний захист від повторної обробки забезпечує
        // атомарна умова під час оновлення документів.
        var alreadyProcessed = await _processedEvents
            .Find(Builders<BsonDocument>.Filter.Eq("_id", eventId))
            .AnyAsync(cancellationToken);

        if (alreadyProcessed)
        {
            logger.LogInformation(
                "Duplicate event {EventId} skipped. MenuItemId: {MenuItemId}",
                eventId,
                e.MenuItemId);

            return;
        }

        // 3. Вибираємо лише відгуки потрібного товару,
        // для яких ця подія новіша за останню застосовану.
        var filterBuilder = Builders<Review>.Filter;

        var filter = filterBuilder.And(
            filterBuilder.Eq(r => r.MenuItemId, e.MenuItemId),
            filterBuilder.Or(
                filterBuilder.Exists(
                    "catalogNameUpdatedAtTicks",
                    false),

                filterBuilder.Lt(
                    "catalogNameUpdatedAtTicks",
                    occurredAtTicks)
            )
        );

        // 4. Атомарно оновлюємо документи.
        // Якщо документ уже має новішу або таку саму версію назви,
        // він не потрапить під фільтр.
        var update = Builders<Review>.Update
            .Set("menuItemName", newName)
            .Set("catalogNameUpdatedAtTicks", occurredAtTicks)
            .Set("updatedAt", DateTime.UtcNow)
            .Inc("version", 1);

        var result = await _reviews.UpdateManyAsync(
            filter,
            update,
            cancellationToken: cancellationToken);

        // 5. Реєструємо успішно оброблену подію.
        // _id у MongoDB має унікальний індекс автоматично.
        var processedEvent = new BsonDocument
        {
            { "_id", eventId },
            { "eventType", nameof(MenuItemRenamedEvent) },
            { "menuItemId", e.MenuItemId },
            { "oldName", e.OldName },
            { "newName", newName },
            { "occurredAt", occurredAt },
            { "occurredAtTicks", occurredAtTicks },
            { "processedAt", DateTime.UtcNow },
            { "matchedReviews", result.MatchedCount },
            { "modifiedReviews", result.ModifiedCount }
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
            // Інший екземпляр consumer уже зареєстрував EventId.
            // Повторне оновлення документів не відбулося,
            // оскільки воно захищене timestamp-фільтром.
            logger.LogInformation(
                "Event {EventId} was already registered by another consumer",
                eventId);
        }

        // 6. Логуємо результат обробки.
        logger.LogInformation(
            "Product renamed event processed. " +
            "EventId: {EventId}, MenuItemId: {MenuItemId}, " +
            "NewName: {NewName}, MatchedReviews: {MatchedCount}, " +
            "ModifiedReviews: {ModifiedCount}",
            eventId,
            e.MenuItemId,
            newName,
            result.MatchedCount,
            result.ModifiedCount);
    }
}
