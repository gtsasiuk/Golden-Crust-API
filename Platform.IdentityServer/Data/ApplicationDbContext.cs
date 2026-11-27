using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace Platform.IdentityServer.Data;

// Користувачі IdentityServer: окрема схема в IdentityServerDb, не залежить від Auth.Api
public class ApplicationUser : IdentityUser;

public class ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
    : IdentityDbContext<ApplicationUser>(options);