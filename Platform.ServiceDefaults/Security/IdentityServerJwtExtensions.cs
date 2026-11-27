using System.IdentityModel.Tokens.Jwt;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.Tokens;

namespace Platform.ServiceDefaults;

public static class IdentityServerJwtExtensions
{
    private const string SmartScheme = "SmartJwt";
    private const string LocalScheme = "LocalJwt";
    private const string IdentityServerScheme = "IdentityServerJwt";

    public static IServiceCollection AddLocalAndIdentityServerJwt(
        this IServiceCollection services,
        IConfiguration configuration,
        string audience,
        params string[] scopes)
    {
        var localJwt = configuration.GetLocalJwtSettings();

        var authority = configuration["IdentityServer:Authority"]
            ?? throw new InvalidOperationException(
                "IdentityServer:Authority is missing.");

        var issuer = configuration["IdentityServer:Issuer"]
            ?? throw new InvalidOperationException(
                "IdentityServer:Issuer is missing.");

        services
            .AddAuthentication(options =>
            {
                options.DefaultAuthenticateScheme = SmartScheme;
                options.DefaultChallengeScheme = SmartScheme;
                options.DefaultForbidScheme = SmartScheme;
            })
            .AddPolicyScheme(
                SmartScheme,
                "Local JWT or Duende IdentityServer JWT",
                options =>
                {
                    options.ForwardDefaultSelector = context =>
                    {
                        var authorization = context.Request.Headers.Authorization
                            .ToString();

                        if (!authorization.StartsWith(
                                "Bearer ",
                                StringComparison.OrdinalIgnoreCase))
                        {
                            return LocalScheme;
                        }

                        var token = authorization["Bearer ".Length..].Trim();

                        try
                        {
                            var handler = new JwtSecurityTokenHandler();

                            if (!handler.CanReadToken(token))
                                return LocalScheme;

                            var tokenIssuer = handler.ReadJwtToken(token).Issuer;

                            return string.Equals(
                                tokenIssuer,
                                issuer,
                                StringComparison.OrdinalIgnoreCase)
                                ? IdentityServerScheme
                                : LocalScheme;
                        }
                        catch
                        {
                            return LocalScheme;
                        }
                    };
                })
            .AddJwtBearer(LocalScheme, options =>
            {
                options.MapInboundClaims = false;
                options.TokenValidationParameters =
                    localJwt.ToValidationParameters();
            })
            .AddJwtBearer(IdentityServerScheme, options =>
            {
                options.Authority = authority;
                options.Audience = audience;
                options.RequireHttpsMetadata = authority.StartsWith(
                    "https://",
                    StringComparison.OrdinalIgnoreCase);

                options.MapInboundClaims = false;

                options.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidateIssuer = true,
                    ValidIssuer = issuer,

                    ValidateAudience = true,
                    ValidAudience = audience,

                    ValidateLifetime = true,
                    ValidateIssuerSigningKey = true,

                    NameClaimType = "name",
                    RoleClaimType = "role",

                    ClockSkew = TimeSpan.FromSeconds(30)
                };
            });

        services.AddAuthorization(options =>
        {
            foreach (var scope in scopes)
            {
                options.AddPolicy(scope, policy =>
                {
                    policy.RequireAuthenticatedUser();
                    policy.RequireClaim("scope", scope);
                });
            }
        });

        return services;
    }
}
