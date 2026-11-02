using ApexPost.Domain.Entities;
using ApexPost.Domain.ValueObjects;
using ApexPost.Application;
using ApexPost.Infrastructure.Persistence;
using ApexPost.Infrastructure.Persistence.SqlEntities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace ApexPost.Application.Tests.Persistence;

public sealed class SqlPersistenceTests
{
    private static ApexPostDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<ApexPostDbContext>()
            .UseSqlServer("Server=(localdb)\\MSSQLLocalDB;Database=ApexPostTests;Trusted_Connection=True;Encrypt=False")
            .Options;
        return new ApexPostDbContext(options);
    }

    [Fact]
    public void Model_UsesRowVersionsAndUniqueReversalMovement()
    {
        using var context = CreateContext();
        var stockVersion = context.Model.FindEntityType(typeof(InventoryStock))!
            .FindProperty("RowVersion")!;
        var transactionVersion = context.Model.FindEntityType(typeof(CounterTransactionRecord))!
            .FindProperty(nameof(CounterTransactionRecord.RowVersion))!;
        var reversalIndex = context.Model.FindEntityType(typeof(StockMovementRecord))!
            .GetIndexes().Single(index => index.GetDatabaseName() == "UX_StockMovements_Reversal");

        Assert.True(stockVersion.IsConcurrencyToken);
        Assert.True(transactionVersion.IsConcurrencyToken);
        Assert.True(reversalIndex.IsUnique);
        Assert.Contains("ReversalRestore", reversalIndex.GetFilter());
    }

    [Fact]
    public void SqlRegistrations_KeepRepositoryCacheAndUnitOfWorkWithinOneScope()
    {
        var services = new ServiceCollection();
        services.AddApplication();
        services.AddSqlInfrastructure(new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:ApexPost"] = "Server=(localdb)\\MSSQLLocalDB;Database=ApexPost_SqlRegistration;Trusted_Connection=True;Encrypt=False"
            })
            .Build());

        Assert.Equal(ServiceLifetime.Scoped,
            services.Single(descriptor => descriptor.ServiceType == typeof(ApexPost.Application.Services.Products.ProductCatalogCache)).Lifetime);
        Assert.Equal(ServiceLifetime.Scoped,
            services.Single(descriptor => descriptor.ServiceType == typeof(ApexPost.Application.Abstractions.Repositories.IProductRepository)).Lifetime);
        Assert.Equal(ServiceLifetime.Scoped,
            services.Single(descriptor => descriptor.ServiceType == typeof(ApexPost.Application.Abstractions.Persistence.IUnitOfWork)).Lifetime);

        using var provider = services.BuildServiceProvider(new ServiceProviderOptions { ValidateScopes = true });
        using var scope = provider.CreateScope();
        var cache = scope.ServiceProvider.GetRequiredService<ApexPost.Application.Services.Products.ProductCatalogCache>();
        var repository = scope.ServiceProvider.GetRequiredService<ApexPost.Application.Abstractions.Repositories.IProductRepository>();
        var auditStore = scope.ServiceProvider.GetRequiredService<ApexPost.Application.Abstractions.Persistence.IAuditStore>();
        var contextFactory = scope.ServiceProvider.GetRequiredService<IDbContextFactory<ApexPostDbContext>>();
        Assert.NotNull(cache);
        Assert.NotNull(repository);
        Assert.NotNull(auditStore);
        Assert.NotNull(contextFactory);
    }

    [Fact]
    public void TransactionMapper_PreservesMissingHistoricalTimeAndValidatesTotals()
    {
        var transaction = new CounterTransaction("TX-1", new BranchCode("IND01"), "C1", "S1",
            [new TaxRate(TaxCategory.Standard, 0.18m)], occurredAtUtc: null);
        transaction.AddLineItem(new TransactionLineItem("Envelope", "Retail", 2, new Money(10m), TaxCategory.Standard, "ENV1"));
        transaction.CompletePayment(PaymentMethod.Cash);

        var record = SqlEntityMapper.ToRecord(transaction);
        var mapped = SqlEntityMapper.ToDomain(record);

        Assert.Null(record.OccurredAtUtc);
        Assert.Null(mapped.OccurredAtUtc);
        Assert.Equal(transaction.Total.Amount, mapped.Total.Amount);
        record.Total += 1m;
        Assert.Throws<InvalidDataException>(() => SqlEntityMapper.ToDomain(record));
    }

    [Fact]
    public void QueryDiagnostics_ProducesTranslatedSqlForFilteredReportShapes()
    {
        using var context = CreateContext();
        var sql = SqlQueryDiagnostics.Capture(context);

        Assert.Equal(3, sql.Count);
        Assert.Contains("BranchCode", sql["Filtered shipment volume"]);
        Assert.Contains("GROUP BY", sql["Filtered shipment volume"]);
        Assert.Contains("MoneyOrders", sql["Money-order activity by branch"]);
        Assert.Contains("IdentityVerifications", sql["Identity verification activity by branch"]);
    }
}
