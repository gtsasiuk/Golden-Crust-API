using System.Diagnostics;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Serilog.Context;

namespace Platform.ServiceDefaults;

public class CorrelationIdMiddleware(RequestDelegate next, ILogger<CorrelationIdMiddleware> logger)
{
    public const string HeaderName = "X-Correlation-Id";

    public async Task InvokeAsync(HttpContext context)
    {
        var generated = false;
        var id = context.Request.Headers[HeaderName].ToString();
        if (string.IsNullOrWhiteSpace(id))
        {
            id = Guid.NewGuid().ToString();
            generated = true;
            context.Request.Headers[HeaderName] = id;
        }

        context.Response.Headers[HeaderName] = id;
        Activity.Current?.SetTag("correlation.id", id);

        var quiet = context.Request.Path.StartsWithSegments("/health")
                 || context.Request.Path.StartsWithSegments("/alive");

        using (LogContext.PushProperty("CorrelationId", id))
        {
            if (generated && !quiet)
                logger.LogInformation("CorrelationId was not provided, generated a new one");
            if (!quiet)
                logger.LogInformation("Request started {Method} {Path}",
                    context.Request.Method, context.Request.Path);

            await next(context);

            if (!quiet)
                logger.LogInformation("Request finished with {StatusCode}", context.Response.StatusCode);
        }
    }
}

public class CorrelationIdHandler(IHttpContextAccessor accessor) : DelegatingHandler
{
    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
    {
        var id = accessor.HttpContext?.Request.Headers[CorrelationIdMiddleware.HeaderName].ToString();
        if (!string.IsNullOrEmpty(id))
            request.Headers.TryAddWithoutValidation(CorrelationIdMiddleware.HeaderName, id);
        return base.SendAsync(request, ct);
    }
}