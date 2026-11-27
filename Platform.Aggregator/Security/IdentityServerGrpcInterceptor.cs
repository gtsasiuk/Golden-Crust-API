
using Grpc.Core;
using Grpc.Core.Interceptors;

namespace Platform.Aggregator.Security;

public sealed class IdentityServerGrpcInterceptor(
    IdentityServerTokenProvider tokenProvider)
    : Interceptor
{
    public override AsyncUnaryCall<TResponse> AsyncUnaryCall<TRequest, TResponse>(
        TRequest request,
        ClientInterceptorContext<TRequest, TResponse> context,
        AsyncUnaryCallContinuation<TRequest, TResponse> continuation)
    {
        var callTask = InvokeAsync();

        return new AsyncUnaryCall<TResponse>(
            callTask.ContinueWith(
                t => t.GetAwaiter().GetResult().ResponseAsync,
                CancellationToken.None,
                TaskContinuationOptions.ExecuteSynchronously,
                TaskScheduler.Default).Unwrap(),

            callTask.ContinueWith(
                t => t.GetAwaiter().GetResult().ResponseHeadersAsync,
                CancellationToken.None,
                TaskContinuationOptions.ExecuteSynchronously,
                TaskScheduler.Default).Unwrap(),

            () => callTask.GetAwaiter().GetResult().GetStatus(),

            () => callTask.GetAwaiter().GetResult().GetTrailers(),

            () =>
            {
                if (callTask.IsCompletedSuccessfully)
                    callTask.Result.Dispose();
                else
                    _ = callTask.ContinueWith(
                        t =>
                        {
                            if (t.IsCompletedSuccessfully)
                                t.Result.Dispose();
                        },
                        CancellationToken.None,
                        TaskContinuationOptions.ExecuteSynchronously,
                        TaskScheduler.Default);
            });

        async Task<AsyncUnaryCall<TResponse>> InvokeAsync()
        {
            var token = await tokenProvider.GetAccessTokenAsync(
                context.Options.CancellationToken);

            var headers = context.Options.Headers is null
                ? new Metadata()
                : CopyHeaders(context.Options.Headers);

            headers.Add("Authorization", $"Bearer {token}");

            var newContext = new ClientInterceptorContext<TRequest, TResponse>(
                context.Method,
                context.Host,
                context.Options.WithHeaders(headers));

            return continuation(request, newContext);
        }
    }

    private static Metadata CopyHeaders(Metadata source)
    {
        var result = new Metadata();

        foreach (var entry in source)
        {
            if (entry.IsBinary)
                result.Add(entry.Key, entry.ValueBytes);
            else
                result.Add(entry.Key, entry.Value);
        }

        return result;
    }
}
