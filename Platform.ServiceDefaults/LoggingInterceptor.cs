using System.Diagnostics;
using Grpc.Core;
using Grpc.Core.Interceptors;
using Microsoft.Extensions.Logging;

namespace Platform.ServiceDefaults;

public class LoggingInterceptor(ILogger<LoggingInterceptor> logger) : Interceptor
{
    public override async Task<TResponse> UnaryServerHandler<TRequest, TResponse>(
        TRequest request, ServerCallContext context, UnaryServerMethod<TRequest, TResponse> continuation)
    {
        var sw = Stopwatch.StartNew();
        try
        {
            var response = await continuation(request, context);
            logger.LogInformation("gRPC {GrpcMethod} from {Peer}: {GrpcStatus} in {ElapsedMs} ms",
                context.Method, context.Peer, nameof(StatusCode.OK), sw.ElapsedMilliseconds);
            return response;
        }
        catch (RpcException ex)
        {
            logger.LogWarning(ex, "gRPC {GrpcMethod} from {Peer}: {GrpcStatus} in {ElapsedMs} ms",
                context.Method, context.Peer, ex.StatusCode, sw.ElapsedMilliseconds);
            throw;
        }
        catch (OperationCanceledException) when (context.CancellationToken.IsCancellationRequested)
        {
            logger.LogWarning("gRPC {GrpcMethod} from {Peer}: {GrpcStatus} in {ElapsedMs} ms",
                context.Method, context.Peer, nameof(StatusCode.Cancelled), sw.ElapsedMilliseconds);
            throw;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "gRPC {GrpcMethod} from {Peer}: unhandled {ExceptionType} in {ElapsedMs} ms",
                context.Method, context.Peer, ex.GetType().Name, sw.ElapsedMilliseconds);
            throw;
        }
    }
}