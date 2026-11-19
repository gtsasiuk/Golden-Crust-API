namespace Reviews.Application.Metrics;

public interface IReviewMetrics
{
    void ReviewCreated(string type);
    void ReviewUpdated();
    void ReviewDeleted();
    void CommentAdded();
    void RecordDuration(string operation, bool success, double milliseconds);
}