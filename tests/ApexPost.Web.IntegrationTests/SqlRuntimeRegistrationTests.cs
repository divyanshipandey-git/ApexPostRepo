using ApexPost.Application.Abstractions.Persistence;
using ApexPost.Application.Abstractions.Repositories;
using ApexPost.Application.Abstractions.Reports;
using ApexPost.Infrastructure.Persistence;
using ApexPost.Infrastructure.Persistence.Repositories;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace ApexPost.Web.IntegrationTests;

public sealed class SqlRuntimeRegistrationTests
{
    [Fact]
    public void WebRuntime_ResolvesSqlPersistenceAndDoesNotRegisterJsonRepositories()
    {
        TestSqlRuntimeSettings.EnsureConfigured();
        using var factory = new WebApplicationFactory<Program>();

        using var scope = factory.Services.CreateScope();
        var services = scope.ServiceProvider;

        Assert.IsType<SqlUnitOfWork>(services.GetRequiredService<IUnitOfWork>());
        Assert.Same(services.GetRequiredService<ApexPostDbContext>(), services.GetRequiredService<ApexPostDbContext>());
        Assert.IsType<SqlAuditStore>(services.GetRequiredService<IAuditStore>());
        Assert.IsType<SqlBranchRepository>(services.GetRequiredService<IBranchRepository>());
        Assert.IsType<SqlCounterRepository>(services.GetRequiredService<ICounterRepository>());
        Assert.IsType<SqlStaffRepository>(services.GetRequiredService<IStaffRepository>());
        Assert.IsType<SqlStaffCredentialRepository>(services.GetRequiredService<IStaffCredentialRepository>());
        Assert.IsType<SqlProductRepository>(services.GetRequiredService<IProductRepository>());
        Assert.IsType<SqlInventoryRepository>(services.GetRequiredService<IInventoryRepository>());
        Assert.Same(services.GetRequiredService<IInventoryRepository>(), services.GetRequiredService<IStockRepository>());
        Assert.IsType<SqlCounterTransactionRepository>(services.GetRequiredService<ICounterTransactionRepository>());
        Assert.IsType<SqlShipmentRepository>(services.GetRequiredService<IShipmentRepository>());
        Assert.IsType<SqlMoneyOrderRepository>(services.GetRequiredService<IMoneyOrderRepository>());
        Assert.IsType<SqlReportDataSource>(services.GetRequiredService<IReportDataSource>());

        Assert.Null(services.GetService<JsonPersistenceOptions>());
        Assert.Null(services.GetService<JsonAuditLog>());
        Assert.Null(services.GetService<JsonReportDataSource>());
        Assert.Null(services.GetService<JsonBranchRepository>());
        Assert.Null(services.GetService<JsonCounterRepository>());
        Assert.Null(services.GetService<JsonStaffRepository>());
        Assert.Null(services.GetService<JsonStaffCredentialRepository>());
        Assert.Null(services.GetService<JsonProductRepository>());
        Assert.Null(services.GetService<JsonInventoryRepository>());
        Assert.Null(services.GetService<JsonCounterTransactionRepository>());
        Assert.Null(services.GetService<JsonShipmentRepository>());
        Assert.Null(services.GetService<JsonMoneyOrderRepository>());
    }
}

internal static class TestSqlRuntimeSettings
{
    private static int _configured;

    public static void EnsureConfigured()
    {
        if (Interlocked.Exchange(ref _configured, 1) == 0)
        {
            Environment.SetEnvironmentVariable(
                "APEXPOST_SQL_CONNECTION",
                "Server=(localdb)\\MSSQLLocalDB;Database=ApexPost_WebCompositionOnly;Trusted_Connection=True;Encrypt=False");
        }
    }
}
