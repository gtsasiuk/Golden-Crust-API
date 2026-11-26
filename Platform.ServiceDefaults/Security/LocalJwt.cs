using System.Text;
using Microsoft.Extensions.Configuration;
using Microsoft.IdentityModel.Tokens;

namespace Platform.ServiceDefaults;

// JWT, який видає Auth.Api. Issuer, Audience і SecretKey приходять з AppHost.
public sealed class LocalJwtSettings
{
    public const string SectionName = "Jwt";

    public string Issuer { get; set; } = string.Empty;
    public string Audience { get; set; } = string.Empty;
    public string SecretKey { get; set; } = string.Empty;
}

public static class LocalJwtExtensions
{
    public static LocalJwtSettings GetLocalJwtSettings(this IConfiguration configuration)
    {
        var settings = configuration.GetSection(LocalJwtSettings.SectionName).Get<LocalJwtSettings>()
                       ?? new LocalJwtSettings();

        if (string.IsNullOrWhiteSpace(settings.Issuer) || string.IsNullOrWhiteSpace(settings.Audience))
            throw new InvalidOperationException("Jwt:Issuer and Jwt:Audience are required");

        if (Encoding.UTF8.GetByteCount(settings.SecretKey) < 32)
            throw new InvalidOperationException("Jwt:SecretKey must be at least 32 bytes (256 bits)");

        return settings;
    }

    public static TokenValidationParameters ToValidationParameters(this LocalJwtSettings settings) => new()
    {
        ValidateIssuer = true,
        ValidIssuer = settings.Issuer,
        ValidateAudience = true,
        ValidAudience = settings.Audience,
        ValidateIssuerSigningKey = true,
        IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(settings.SecretKey)),
        ValidateLifetime = true,
        ClockSkew = TimeSpan.FromSeconds(30),

        // Ті самі імена claims, що й в Auth.Api (крок 1)
        NameClaimType = "unique_name",
        RoleClaimType = "role"
    };
}