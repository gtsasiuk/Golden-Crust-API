using Microsoft.Extensions.Options;
using MongoDB.Driver;
using Reviews.Domain.Entities;
using MongoDB.Driver.Core.Extensions.DiagnosticSources;

namespace Reviews.Infrastructure.Persistence;

public class MongoDbContext
{
    public const string ReviewsCollection = "reviews";
    public const string DiscussionsCollection = "discussions";

    public MongoDbContext(IOptions<MongoDbOptions> options)
    {
        var o = options.Value;
        if (string.IsNullOrWhiteSpace(o.ConnectionString) || string.IsNullOrWhiteSpace(o.DatabaseName))
            throw new InvalidOperationException(
                "MongoDb:ConnectionString and MongoDb:DatabaseName must be configured");

        var settings = MongoClientSettings.FromConnectionString(o.ConnectionString);
        settings.ClusterConfigurator = cb => cb.Subscribe(new DiagnosticsActivityEventSubscriber());
        var client = new MongoClient(settings);
        Database = client.GetDatabase(o.DatabaseName);
    }

    public IMongoDatabase Database { get; }

    public IMongoCollection<Review> Reviews => Database.GetCollection<Review>(ReviewsCollection);
    public IMongoCollection<Discussion> Discussions => Database.GetCollection<Discussion>(DiscussionsCollection);

    internal IMongoCollection<ReviewReadModel> ReviewReadModels
        => Database.GetCollection<ReviewReadModel>(ReviewsCollection);
}