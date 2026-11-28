using Auth.Api.Models;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace Auth.Api.Data;

public class ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
    : IdentityDbContext<ApplicationUser>(options)
{
    public DbSet<RefreshToken> RefreshTokens => Set<RefreshToken>();

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);

        builder.Entity<RefreshToken>(e =>
        {
            e.ToTable("RefreshTokens");
            e.HasKey(t => t.Id);

            e.Property(t => t.TokenHash).IsRequired().HasMaxLength(128);
            e.HasIndex(t => t.TokenHash).IsUnique();

            e.Property(t => t.CreatedByIp).HasMaxLength(64);
            e.Property(t => t.RevokedByIp).HasMaxLength(64);
            e.Property(t => t.ReplacedByTokenHash).HasMaxLength(128);
            e.Property(t => t.RevokeReason).HasMaxLength(200);

            e.HasOne(t => t.User)
             .WithMany(u => u.RefreshTokens)
             .HasForeignKey(t => t.UserId)
             .OnDelete(DeleteBehavior.Cascade);
        });
    }
}