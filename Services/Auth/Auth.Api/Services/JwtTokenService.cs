using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using Auth.Api.Models;
using Auth.Api.Options;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;

namespace Auth.Api.Services;

public sealed class JwtTokenService : IJwtTokenService
{
    private readonly JwtSettings _settings;
    private readonly SymmetricSecurityKey _key;
    private readonly JsonWebTokenHandler _handler = new();
    private readonly TokenValidationParameters _validationParameters;

    public JwtTokenService(IOptions<JwtSettings> options)
    {
        _settings = options.Value;
        _key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_settings.SecretKey));

        _validationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidIssuer = _settings.Issuer,
            ValidateAudience = true,
            ValidAudience = _settings.Audience,
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = _key,
            ValidateLifetime = true,
            ClockSkew = TimeSpan.FromSeconds(30),
            NameClaimType = "unique_name",
            RoleClaimType = "role"
        };
    }

    public AccessToken GenerateAccessToken(ApplicationUser user, IEnumerable<string> roles)
    {
        var now = DateTime.UtcNow;
        var expires = now.AddMinutes(_settings.AccessTokenMinutes);

        var claims = new List<Claim>
        {
            new("sub", user.Id),
            new("unique_name", user.UserName!),
            new("email", user.Email!),
            new("jti", Guid.NewGuid().ToString())
        };
        claims.AddRange(roles.Select(r => new Claim("role", r)));

        var descriptor = new SecurityTokenDescriptor
        {
            Subject = new ClaimsIdentity(claims),
            Issuer = _settings.Issuer,
            Audience = _settings.Audience,
            IssuedAt = now,
            NotBefore = now,
            Expires = expires,
            SigningCredentials = new SigningCredentials(_key, SecurityAlgorithms.HmacSha256)
        };

        return new AccessToken(_handler.CreateToken(descriptor), expires);
    }

    public RefreshTokenValue GenerateRefreshToken()
    {
        var raw = WebEncoders.Base64UrlEncode(RandomNumberGenerator.GetBytes(64));
        return new RefreshTokenValue(raw, HashToken(raw), DateTime.UtcNow.AddDays(_settings.RefreshTokenDays));
    }

    public async Task<ClaimsPrincipal?> ValidateTokenAsync(string token)
    {
        var result = await _handler.ValidateTokenAsync(token, _validationParameters);
        return result.IsValid ? new ClaimsPrincipal(result.ClaimsIdentity) : null;
    }

    public string HashToken(string rawToken)
        => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(rawToken)));
}