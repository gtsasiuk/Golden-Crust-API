using System.Diagnostics.Metrics;
using Reviews.Application.Metrics;
using Reviews.Infrastructure.Persistence;
using Platform.ServiceDefaults;

namespace Reviews.WebApi.Metrics;

public sealed class ReviewsSnapshot
{
    private long _total;
    public long Total => Interlocked.Read(ref _total);
    public void Update(long value) => Interlocked.Exchange(ref _total, value);
}

public sealed class ReviewMetrics : IReviewMetrics
{
    public const string MeterName = "Reviews.Api";

    private readonly Counter<long> _created;
    private readonly Counter<long> _updated;
    private readonly Counter<long> _deleted;
    private readonly Counter<long> _comments;
    private readonly Histogram<double> _duration;

    public ReviewMetrics(IMeterFactory meterFactory, ReviewsSnapshot snapshot)
    {
        var meter = meterFactory.Create(MeterName);

        _created = meter.CreateCounter<long>("reviews.created", unit: "{review}", description: "Created reviews, by review type");
        _updated = meter.CreateCounter<long>("reviews.updated", unit: "{review}", description: "Edited reviews");
        _deleted = meter.CreateCounter<long>("reviews.deleted", unit: "{review}", description: "Deleted reviews");
        _comments = meter.CreateCounter<long>("reviews.comments.added", unit: "{comment}", description: "Added comments");
        _duration = meter.CreateHistogram<double>("reviews.operation.duration", unit: "ms",
            description: "Duration of review commands and queries");

        meter.CreateObservableGauge("reviews.total", () => snapshot.Total,
            unit: "{review}", description: "Reviews currently stored");
    }

    public void ReviewCreated(string type) =>
        _created.Add(1, new KeyValuePair<string, object?>("review.type", type));

    public void ReviewUpdated() => _updated.Add(1);
    public void ReviewDeleted() => _deleted.Add(1);
    public void CommentAdded() => _comments.Add(1);

    public void RecordDuration(string operation, bool success, double milliseconds) =>
        _duration.Record(milliseconds,
            new KeyValuePair<string, object?>("operation", operation),
            new KeyValuePair<string, object?>("result", success ? "success" : "failure"));
}

public sealed class ReviewsSnapshotRefresher(
    MongoDbContext context, ReviewsSnapshot snapshot, ILogger<ReviewsSnapshotRefresher> logger)
    : PeriodicRefreshService(logger, TimeSpan.FromSeconds(15))
{
    protected override async Task RefreshAsync(CancellationToken ct)
        => snapshot.Update(await context.Reviews.EstimatedDocumentCountAsync(cancellationToken: ct));
}