using System.Text.Json;
using System.Text.Json.Serialization;
using Reviews.Application;
using Reviews.Application.Reviews.Mappings;
using Reviews.Infrastructure;
using Reviews.WebApi.ErrorHandling;
using Reviews.WebApi.Mapping;
using Serilog;
using Serilog.Context;
using Serilog.Formatting.Compact;

var builder = WebApplication.CreateBuilder(args);

builder.Host.UseSerilog((ctx, cfg) => cfg
    .ReadFrom.Configuration(ctx.Configuration)
    .Enrich.FromLogContext()
    .Enrich.WithProperty("Service", "reviews-api")
    .WriteTo.Console()
    .WriteTo.File(new CompactJsonFormatter(), "logs/reviews-.json", rollingInterval: RollingInterval.Day));

builder.Services
    .AddApplication(builder.Configuration)
    .AddInfrastructure(builder.Configuration);

builder.Services.AddAutoMapper(cfg =>
{
    cfg.LicenseKey = builder.Configuration["LuckyPenny:LicenseKey"];
    cfg.AddMaps(typeof(ReviewProfile).Assembly);
    cfg.AddProfile<RequestProfile>();
});

builder.Services.AddControllers()
    .AddJsonOptions(o => o.JsonSerializerOptions.Converters.Add(
        new JsonStringEnumConverter(JsonNamingPolicy.CamelCase)));

builder.Services.AddProblemDetails(o => o.CustomizeProblemDetails = ctx =>
    ctx.ProblemDetails.Extensions["traceId"] = ctx.HttpContext.TraceIdentifier);
builder.Services.AddExceptionHandler<ApiExceptionHandler>();

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

var app = builder.Build();

app.Use(async (ctx, next) =>
{
    var correlationId = ctx.Request.Headers["X-Correlation-Id"].FirstOrDefault() ?? ctx.TraceIdentifier;
    ctx.Response.Headers["X-Correlation-Id"] = correlationId;
    using (LogContext.PushProperty("CorrelationId", correlationId))
        await next();
});

app.UseSerilogRequestLogging(o => o.EnrichDiagnosticContext = (diag, http) =>
    diag.Set("UserId", http.User.Identity?.Name ?? "anonymous"));
app.UseExceptionHandler();
app.UseStatusCodePages();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();

    using var mapScope = app.Services.CreateScope();
    mapScope.ServiceProvider.GetRequiredService<AutoMapper.IMapper>()
            .ConfigurationProvider.AssertConfigurationIsValid();
}

app.MapControllers();
app.Run();