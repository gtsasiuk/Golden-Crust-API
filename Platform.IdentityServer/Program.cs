using Duende.IdentityServer.EntityFramework.DbContexts;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Platform.IdentityServer.Data;
using Platform.ServiceDefaults;

var builder = WebApplication.CreateBuilder(args);

builder.AddServiceDefaults();

// ── База: окрема від бізнес-сервісів (ізоляція безпеки) ───────────────────────
// Рядок підключення дає Aspire через WithReference. Під час `dotnet ef` його немає:
// тоді працюємо в design-time режимі (без звернення до БД).
var configuredConnectionString = builder.Configuration.GetConnectionString("IdentityServerDb");
var designTime = string.IsNullOrWhiteSpace(configuredConnectionString);
var connectionString = configuredConnectionString
    ?? "Server=(local);Database=IdentityServerDb_DesignTime;Trusted_Connection=True;TrustServerCertificate=True";

// Міграції лежать у цьому проєкті, а не в збірках Duende
var migrationsAssembly = typeof(Program).Assembly.GetName().Name;

builder.Services.AddHealthChecks()
    .AddSqlServer(
        connectionString: connectionString,
        name: "identityserver-db",
        failureStatus: HealthStatus.Unhealthy,
        tags: ["database", "sql", "ready"],
        timeout: TimeSpan.FromSeconds(5));

builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseSqlServer(connectionString, sql => sql.MigrationsAssembly(migrationsAssembly)));

// ── ASP.NET Identity: користувачі IdentityServer ──────────────────────────────
builder.Services.AddIdentity<ApplicationUser, IdentityRole>(options =>
{
    options.Password.RequiredLength = 8;
    options.Password.RequireDigit = true;
    options.Password.RequireLowercase = true;
    options.Password.RequireUppercase = true;
    options.Password.RequireNonAlphanumeric = true;

    options.Lockout.AllowedForNewUsers = true;
    options.Lockout.MaxFailedAccessAttempts = 5;
    options.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(15);

    options.User.RequireUniqueEmail = true;
})
.AddEntityFrameworkStores<ApplicationDbContext>()
.AddDefaultTokenProviders();

// ── Duende IdentityServer + ASP.NET Identity + EF-сховища ─────────────────────
builder.Services.AddIdentityServer(options =>
{
    // Фіксований issuer: однаковий у токенах і в discovery document
    options.IssuerUri = builder.Configuration["IdentityServer:IssuerUri"];

    // dev працює по http: SameSite=None без Secure браузер відкидає
    options.Authentication.CookieSameSiteMode = SameSiteMode.Lax;

    options.Events.RaiseErrorEvents = true;
    options.Events.RaiseInformationEvents = true;
    options.Events.RaiseFailureEvents = true;
    options.Events.RaiseSuccessEvents = true;
})
.AddAspNetIdentity<ApplicationUser>()
.AddConfigurationStore(options =>
    options.ConfigureDbContext = b =>
        b.UseSqlServer(connectionString, sql => sql.MigrationsAssembly(migrationsAssembly)))
.AddOperationalStore(options =>
{
    options.ConfigureDbContext = b =>
        b.UseSqlServer(connectionString, sql => sql.MigrationsAssembly(migrationsAssembly));

    options.EnableTokenCleanup = true;
    options.TokenCleanupInterval = 3600;
});

builder.Services.AddRazorPages();

var app = builder.Build();

app.UseMiddleware<CorrelationIdMiddleware>();

if (!designTime)
    await IdentityServerInitializer.InitializeAsync(app.Services, app.Configuration, app.Logger);

if (!app.Environment.IsDevelopment())
    app.UseExceptionHandler("/Error");

app.UseStaticFiles();
app.UseRouting();

app.UseIdentityServer();   // вмикає автентифікацію й endpoints IdentityServer
app.UseAuthorization();

app.MapRazorPages();
app.MapDefaultEndpoints();

app.Run();