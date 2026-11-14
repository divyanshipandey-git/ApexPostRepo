using ApexPost.Infrastructure.Persistence;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.DependencyInjection;

namespace ApexPost.IntegrationTests.TestSupport;

/// <summary>
/// Hosts the real ApexPost.Api in memory (real routing, filters, validation, JWT validation, exception handler, EF Core)
/// with ONE change: the SQL Server DbContext registration is replaced by a private in-memory SQLite database.
/// A new factory is created per test, so tests never share data, a clock or a rate-limit window.
/// </summary>
public sealed class ApexApiFactory : WebApplicationFactory<Program>
{
    private readonly SqliteConnection _connection = new("DataSource=:memory:");

    /// <summary>Fake, test-only signing key (not a secret: it only ever exists inside this test process).</summary>
    public const string TestSigningKey = "unit-test-only-signing-key-0123456789-abcdefghij";

    public ApexApiFactory()
    {
        _connection.Open();
        ClientOptions.BaseAddress = new Uri("https://localhost"); // avoids the HTTP to HTTPS redirect
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        // "Testing" instead of "Development": no user-secrets from the developer machine, no /dev/token endpoint.
        builder.UseEnvironment("Testing");
        builder.UseSetting("Jwt:SigningKey", TestSigningKey);
        builder.UseSetting("Jwt:Issuer", TestJwt.Issuer);
        builder.UseSetting("Jwt:Audience", TestJwt.Audience);
        builder.UseSetting("ConnectionStrings:ApexPost", "Server=never-contacted.invalid;Database=none");

        builder.ConfigureServices(services =>
        {
            foreach (var descriptor in services.Where(IsApexPostDbContextRegistration).ToList())
            {
                services.Remove(descriptor);
            }

            Action<DbContextOptionsBuilder> useSqlite = options => options
                .UseSqlite(_connection)
                .ReplaceService<IModelCustomizer, SqliteModelCustomizer>();
            services.AddDbContext<ApexPostDbContext>(useSqlite);
            services.AddDbContextFactory<ApexPostDbContext>(useSqlite, ServiceLifetime.Scoped);

            using var provider = services.BuildServiceProvider();
            using var scope = provider.CreateScope();
            scope.ServiceProvider.GetRequiredService<ApexPostDbContext>().Database.EnsureCreated();
        });
    }

    private static bool IsApexPostDbContextRegistration(ServiceDescriptor descriptor)
    {
        var type = descriptor.ServiceType;
        if (type == typeof(ApexPostDbContext) || type == typeof(DbContextOptions) || type == typeof(DbContextOptions<ApexPostDbContext>)
            || type == typeof(IDbContextFactory<ApexPostDbContext>))
        {
            return true;
        }

        // EF Core 9+ stores the per-context provider configuration as IDbContextOptionsConfiguration<TContext>.
        return type.IsGenericType
            && type.GetGenericTypeDefinition().Name.StartsWith("IDbContextOptionsConfiguration", StringComparison.Ordinal)
            && type.GetGenericArguments()[0] == typeof(ApexPostDbContext);
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);
        if (disposing)
        {
            _connection.Dispose();
        }
    }
}
