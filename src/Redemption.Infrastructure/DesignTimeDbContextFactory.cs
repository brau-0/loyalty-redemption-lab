using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Redemption.Infrastructure;

// Used only by 'dotnet ef' to generate migrations. It never opens a connection.
public sealed class DesignTimeDbContextFactory : IDesignTimeDbContextFactory<RedemptionDbContext>
{
    public RedemptionDbContext CreateDbContext(string[] args)
    {
        var options = new DbContextOptionsBuilder<RedemptionDbContext>()
            .UseSqlServer("Server=localhost;Database=redemptiondb;Trusted_Connection=True;TrustServerCertificate=True")
            .Options;
        return new RedemptionDbContext(options);
    }
}