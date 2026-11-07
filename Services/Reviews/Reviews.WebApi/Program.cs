using System.Text.Json;
using System.Text.Json.Serialization;
using Reviews.Application;
using Reviews.Application.Reviews.Mappings;
using Reviews.Infrastructure;
using Reviews.WebApi.ErrorHandling;
using Reviews.WebApi.GrpcServices;
using Reviews.WebApi.Mapping;
using Platform.ServiceDefaults;

var builder = WebApplication.CreateBuilder(args);

builder.AddServiceDefaults();

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
builder.Services.AddGrpc(o => o.Interceptors.Add<LoggingInterceptor>());
if (builder.Environment.IsDevelopment())
    builder.Services.AddGrpcReflection();

builder.Services.AddProblemDetails(o => o.CustomizeProblemDetails = ctx =>
    ctx.ProblemDetails.Extensions["traceId"] = ctx.HttpContext.TraceIdentifier);
builder.Services.AddExceptionHandler<ApiExceptionHandler>();

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

var app = builder.Build();

app.UseMiddleware<CorrelationIdMiddleware>();
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
app.MapGrpcService<ReviewsGrpcService>();
if (app.Environment.IsDevelopment())
    app.MapGrpcReflectionService();
app.MapDefaultEndpoints();
app.Run();