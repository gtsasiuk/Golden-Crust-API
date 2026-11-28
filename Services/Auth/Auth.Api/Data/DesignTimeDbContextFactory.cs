using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Auth.Api.Data;

public class DesignTimeDbContextFactory : IDesignTimeDbContextFactory<ApplicationDbContext>
{
    public ApplicationDbContext CreateDbContext(string[] args)
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseSqlServer("Server=localhost,1433;Database=GoldenCrust_Identity;User Id=sa;Password=design-time-only;TrustServerCertificate=True")
            .Options;

        return new ApplicationDbContext(options);
    }
}