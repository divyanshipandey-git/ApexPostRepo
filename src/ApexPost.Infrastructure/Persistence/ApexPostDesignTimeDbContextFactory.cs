using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Microsoft.Extensions.Configuration;

namespace ApexPost.Infrastructure.Persistence;

public sealed class ApexPostDesignTimeDbContextFactory : IDesignTimeDbContextFactory<ApexPostDbContext>
{
    public ApexPostDbContext CreateDbContext(string[] args)
    {
        var configuration = new ConfigurationBuilder()
            .AddUserSecrets("ApexPost.CounterPlatform-Development")
            .AddEnvironmentVariables()
            .Build();
        var connectionString = Environment.GetEnvironmentVariable("APEXPOST_SQL_CONNECTION")
            ?? configuration.GetConnectionString("ApexPost");
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            throw new InvalidOperationException(
                "The SQL Server connection string is not configured. Set ConnectionStrings:ApexPost with user-secrets or APEXPOST_SQL_CONNECTION for EF tooling.");
        }
        var options = new DbContextOptionsBuilder<ApexPostDbContext>()
            .UseSqlServer(connectionString, sql => sql.EnableRetryOnFailure())
            .Options;
        return new ApexPostDbContext(options);
    }
}
