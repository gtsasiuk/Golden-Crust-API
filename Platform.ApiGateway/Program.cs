using Microsoft.AspNetCore.Authentication.JwtBearer;
using Platform.ServiceDefaults;
using Yarp.ReverseProxy.Forwarder;
using Yarp.ReverseProxy.Model;
using Microsoft.AspNetCore.Authorization;

var builder = WebApplication.CreateBuilder(args);

builder.AddServiceDefaults();

// ── Автентифікація: Gateway перевіряє JWT ПЕРЕД проксіюванням ────────────────
builder.Services.AddLocalAndIdentityServerJwt(
    builder.Configuration,
    audience: null);

builder.Services.AddAuthentication()
    .AddKeycloakJwtBearer(
        serviceName: "keycloak",
        realm: "DevRealmDrivers",
        authenticationScheme: "Keycloak",
        configureOptions: options =>
        {
            // Gateway перевіряє автентичність токена.
            // Конкретний Audience перевіряє сам мікросервіс.
            options.RequireHttpsMetadata = false;
            options.TokenValidationParameters.ValidateAudience = false;
        });

builder.Services.AddAuthorization(options =>
    options.AddPolicy("authenticated", policy => policy.RequireAuthenticatedUser()));

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

    var proxy = context.Features.Get<IReverseProxyFeature>();
    if (proxy is null)
    {
        app.Logger.LogDebug("{Method} {Path} handled by the gateway itself: {StatusCode} in {Elapsed} ms",
            context.Request.Method, context.Request.Path, context.Response.StatusCode, sw.ElapsedMilliseconds);
        return;
    }
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

app.UseAuthentication();
app.UseAuthorization();

app.MapReverseProxy();
app.MapDefaultEndpoints();
app.Run();