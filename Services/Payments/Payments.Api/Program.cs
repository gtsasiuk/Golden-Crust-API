using Microsoft.AspNetCore.Authentication.JwtBearer;
using Payments.Api.Services;
using Platform.ServiceDefaults;

var builder = WebApplication.CreateBuilder(args);

builder.AddServiceDefaults();

// Keycloak authentication
builder.Services
    .AddAuthentication(options =>
    {
        options.DefaultAuthenticateScheme = "Keycloak";
        options.DefaultChallengeScheme = "Keycloak";
    })
    .AddKeycloakJwtBearer(
        serviceName: "keycloak",
        realm: "DevRealmDrivers",
        authenticationScheme: "Keycloak",
        configureOptions: options =>
        {
            options.Audience = "payments-api";

            if (builder.Environment.IsDevelopment())
                options.RequireHttpsMetadata = false;
        });

builder.Services.AddAuthorization();

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
