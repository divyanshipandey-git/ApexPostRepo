using ApexPost.Application.Abstractions.Persistence;
using ApexPost.Application.Abstractions.Repositories;
using ApexPost.Application.Abstractions.Reports;
using ApexPost.Application.Abstractions.Security;
using ApexPost.Infrastructure.Persistence.Repositories;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using ApexPost.Infrastructure.Security;

namespace ApexPost.Infrastructure.Persistence;

public static class SqlInfrastructureExtensions
{
    public static IServiceCollection AddSqlInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);
        var connectionString = Environment.GetEnvironmentVariable("APEXPOST_SQL_CONNECTION")
            ?? configuration.GetConnectionString("ApexPost");
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            throw new InvalidOperationException(
                "The SQL Server connection string 'ConnectionStrings:ApexPost' is required. " +
                "For Development, configure it with: dotnet user-secrets set \"ConnectionStrings:ApexPost\" \"<connection-string>\" --project src/ApexPost.Web");
        }

        services.AddDbContext<ApexPostDbContext>(options =>
            options.UseSqlServer(connectionString, sql => sql.EnableRetryOnFailure())
                .ConfigureWarnings(w => w.Ignore(RelationalEventId.PendingModelChangesWarning)));
        services.AddDbContextFactory<ApexPostDbContext>(options =>
            options.UseSqlServer(connectionString, sql => sql.EnableRetryOnFailure())
                .ConfigureWarnings(w => w.Ignore(RelationalEventId.PendingModelChangesWarning)),
            lifetime: ServiceLifetime.Scoped);
        services.TryAddScoped<IUnitOfWork, SqlUnitOfWork>();
        services.TryAddScoped<IAuditStore, SqlAuditStore>();
        services.TryAddSingleton<IStaffPasswordHasher, Pbkdf2StaffPasswordHasher>();
        services.TryAddScoped<IBranchRepository, SqlBranchRepository>();
        services.TryAddScoped<ICounterRepository, SqlCounterRepository>();
        services.TryAddScoped<IStaffRepository, SqlStaffRepository>();
        services.TryAddScoped<IStaffCredentialRepository, SqlStaffCredentialRepository>();
        services.TryAddScoped<IProductRepository, SqlProductRepository>();
        services.TryAddScoped<IProductPriceRevisionRepository, SqlProductPriceRevisionRepository>();
        services.TryAddScoped<SqlInventoryRepository>();
        services.TryAddScoped<IInventoryRepository>(provider => provider.GetRequiredService<SqlInventoryRepository>());
        services.TryAddScoped<IStockRepository>(provider => provider.GetRequiredService<SqlInventoryRepository>());
        services.TryAddScoped<ICounterTransactionRepository, SqlCounterTransactionRepository>();
        services.TryAddScoped<IShipmentRepository, SqlShipmentRepository>();
        services.TryAddScoped<ITrackedShipmentRepository, SqlTrackedShipmentRepository>();
        services.TryAddScoped<IMoneyOrderRepository, SqlMoneyOrderRepository>();
        services.TryAddScoped<IReportDataSource, SqlReportDataSource>();
        return services;
    }
}
