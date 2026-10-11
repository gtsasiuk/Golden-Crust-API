using Orders.Api.Infrastructure;
using Orders.Bll.Mapping;
using Orders.Bll.Services;
using Orders.Dal;
using Orders.Dal.Interfaces;
using Serilog;

var builder = WebApplication.CreateBuilder(args);

// Рядок підключення: appsettings.json, а в Aspire/Docker його перевизначає ConnectionStrings__OrdersDb
var connectionString = builder.Configuration.GetConnectionString("OrdersDb")
    ?? throw new InvalidOperationException("Connection string 'OrdersDb' is missing");

builder.Host.UseSerilog((ctx, cfg) => cfg
    .ReadFrom.Configuration(ctx.Configuration)
    .Enrich.FromLogContext()
    .WriteTo.Console());

// DI. Scoped: один UnitOfWork (і одне з'єднання) на HTTP-запит
builder.Services.AddScoped<IUnitOfWork>(_ => new UnitOfWork(connectionString));
builder.Services.AddScoped<ICustomerService, CustomerService>();
builder.Services.AddScoped<IMenuService, MenuService>();
builder.Services.AddScoped<IOrderService, OrderService>();

builder.Services.AddAutoMapper(cfg =>
{
    cfg.LicenseKey = builder.Configuration["AutoMapper:LicenseKey"];   // без ключа працює, лише попередження в лозі
    cfg.AddProfile<OrdersProfile>();
});

builder.Services.AddControllers();

// Помилки: один глобальний обробник + формат ProblemDetails (RFC 9457)
builder.Services.AddProblemDetails(o => o.CustomizeProblemDetails = ctx =>
    ctx.ProblemDetails.Extensions["traceId"] = ctx.HttpContext.TraceIdentifier);
builder.Services.AddExceptionHandler<DomainExceptionHandler>();

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

var app = builder.Build();

app.UseExceptionHandler();
app.UseStatusCodePages();          // порожні 404/405 теж віддаємо як ProblemDetails
app.UseSerilogRequestLogging();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();

    // Рання перевірка мапінгу: помилка в профілі AutoMapper видна на старті, а не під час запиту
    using var scope = app.Services.CreateScope();
    scope.ServiceProvider.GetRequiredService<AutoMapper.IMapper>()
         .ConfigurationProvider.AssertConfigurationIsValid();
}

app.UseHttpsRedirection();
app.MapControllers();
app.Run();