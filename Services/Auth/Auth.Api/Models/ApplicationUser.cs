using Microsoft.AspNetCore.Identity;

namespace Auth.Api.Models;

public class ApplicationUser : IdentityUser
{
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;

    public ICollection<RefreshToken> RefreshTokens { get; set; } = new List<RefreshToken>();
}