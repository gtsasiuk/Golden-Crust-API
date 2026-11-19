using System.Diagnostics;
using MediatR;
using Reviews.Application.Metrics;

namespace Reviews.Application.Common.Behaviors;

public class MetricsBehavior<TRequest, TResponse>(IReviewMetrics metrics)
    : IPipelineBehavior<TRequest, TResponse> where TRequest : notnull
{
    public async Task<TResponse> Handle(
        TRequest request, RequestHandlerDelegate<TResponse> next, CancellationToken ct)
    {
        var operation = typeof(TRequest).Name;
        var sw = Stopwatch.StartNew();
        try
        {
            var response = await next();
            metrics.RecordDuration(operation, true, sw.Elapsed.TotalMilliseconds);
            return response;
        }
        catch
        {
            metrics.RecordDuration(operation, false, sw.Elapsed.TotalMilliseconds);
            throw;
        }
    }
}