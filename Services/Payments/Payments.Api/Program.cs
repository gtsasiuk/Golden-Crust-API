using Microsoft.AspNetCore.Authentication.JwtBearer;
using Payments.Api.Services;
using Platform.ServiceDefaults;
using Platform.ServiceDefaults.Security;

var builder = WebApplication.CreateBuilder(args);

builder.AddServiceDefaults();

builder.Services.AddLocalAndIdentityServerJwt(
    builder.Configuration,
    "payments_api",
    "payments.read");

builder.Services.AddAuthentication()
    .AddKeycloakJwtBearer(
        serviceName: "keycloak",
        realm: "DevRealmDrivers",
        authenticationScheme: "Keycloak",
        configureOptions: options =>
        {
            options.Audience = "payments-api";

            if (builder.Environment.IsDevelopment())
                options.RequireHttpsMetadata = false;

            options.Events = new JwtBearerEvents
            {
                OnTokenValidated = KeycloakRoleClaims.MapRealmRolesAsync
            };
        });

builder.Services.AcceptKeycloakTokens();

builder.Services.AddAuthorization();
builder.Services.AddPermissionAuthorization();

// Payment storage
builder.Services.AddSingleton<PaymentStore>();

// Controllers
builder.Services.AddControllers();

// Swagger
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();

app.MapDefaultEndpoints();

app.Run();
