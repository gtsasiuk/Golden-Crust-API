using Platform.ServiceDefaults;
using Yarp.ReverseProxy.Forwarder;
using Yarp.ReverseProxy.Model;

var builder = WebApplication.CreateBuilder(args);

builder.AddServiceDefaults();

builder.Services.AddReverseProxy()
    .LoadFromConfig(builder.Configuration.GetSection("ReverseProxy"))
    .AddServiceDiscoveryDestinationResolver();

var app = builder.Build();

app.UseMiddleware<CorrelationIdMiddleware>();

app.Use(async (context, next) =>
{
    var sw = System.Diagnostics.Stopwatch.StartNew();
    await next();
    sw.Stop();

    var proxy = context.GetReverseProxyFeature();
    var route = proxy?.Route.Config.RouteId;
    var cluster = proxy?.Cluster.Config.ClusterId;
    var destination = proxy?.ProxiedDestination?.Model.Config.Address;
    var clientIp = context.Connection.RemoteIpAddress?.ToString();

    var error = context.Features.Get<IForwarderErrorFeature>();
    if (error is not null)
    {
        app.Logger.LogWarning(error.Exception,
            "{Method} {Path} -> route {Route}, cluster {Cluster} failed: {ForwarderError}, status {StatusCode} in {Elapsed} ms",
            context.Request.Method, context.Request.Path, route, cluster,
            error.Error, context.Response.StatusCode, sw.ElapsedMilliseconds);
    }
    else
    {
        app.Logger.LogInformation(
            "{Method} {Path} -> route {Route}, cluster {Cluster}, destination {Destination}: {StatusCode} in {Elapsed} ms, client {ClientIp}",
            context.Request.Method, context.Request.Path, route, cluster, destination,
            context.Response.StatusCode, sw.ElapsedMilliseconds, clientIp);
    }
});

app.MapReverseProxy();
app.MapDefaultEndpoints();
app.Run();