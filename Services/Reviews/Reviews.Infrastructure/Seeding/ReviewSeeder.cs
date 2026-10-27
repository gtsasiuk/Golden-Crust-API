using Microsoft.Extensions.Logging;
using Reviews.Domain.Entities;
using Reviews.Domain.Enums;
using Reviews.Domain.ValueObjects;
using Reviews.Infrastructure.Persistence;

namespace Reviews.Infrastructure.Seeding;

public class ReviewSeeder(MongoDbContext context, ILogger<ReviewSeeder> logger) : IDataSeeder
{
    public async Task SeedAsync(CancellationToken ct = default)
    {
        if (await context.Reviews.EstimatedDocumentCountAsync(cancellationToken: ct) > 0)
        {
            logger.LogInformation("Collection 'reviews' already has data, seeding skipped");
            return;
        }

        var olena = new Author(1, "Олена");
        var andriy = new Author(2, "Андрій");
        var iryna = new Author(3, "Ірина");

        var r1 = new Review(1, "Маргарита", olena, ReviewType.Text, 5,
            "Тісто тонке, соус насичений. Приїхала ще гарячою.", orderId: 1);
        r1.AddComment(andriy, "Погоджуюсь, найкраща в місті");
        r1.AddComment(iryna, "А без базиліку можна замовити?");

        var r2 = new Review(1, "Маргарита", andriy, ReviewType.Photo, 4,
            "Смачно, але хотілося б більше сиру.",
            photos: new[]
            {
                "https://cdn.goldencrust.example/reviews/r2-1.jpg",
                "https://cdn.goldencrust.example/reviews/r2-2.jpg"
            });

        var r3 = new Review(1, "Маргарита", iryna, ReviewType.Quick, 5);

        var r4 = new Review(2, "Пепероні", olena, ReviewType.Text, 4, "Добре, але трохи жирна.",
            pros: new[] { "багато пепероні", "хрустке тісто" }, cons: new[] { "жирна" }, orderId: 2);

        var r5 = new Review(2, "Пепероні", andriy, ReviewType.Text, 3, "Нормально, але очікував гостріше.");

        var discussion = new Discussion(1, "Маргарита без базиліку?", iryna, r1.Id);
        r1.AttachDiscussion(discussion.Id);

        await context.Reviews.InsertManyAsync(new[] { r1, r2, r3, r4, r5 }, cancellationToken: ct);

        if (await context.Discussions.EstimatedDocumentCountAsync(cancellationToken: ct) == 0)
            await context.Discussions.InsertOneAsync(discussion, cancellationToken: ct);

        logger.LogInformation("Seeded 5 reviews and 1 discussion");
    }
}