using ApexPost.Application;
using ApexPost.Application.Abstractions.Persistence;
using ApexPost.Application.Abstractions.Repositories;
using ApexPost.Application.Abstractions.Reports;
using ApexPost.Application.Services.Auditing;
using ApexPost.Application.Services.Products;
using ApexPost.Application.Services.Reports;
using ApexPost.Application.Services.Shipping;
using ApexPost.Application.Services.Staff;
using ApexPost.Application.Services.Transactions;
using ApexPost.Domain.Entities;
using ApexPost.Domain.ValueObjects;
using ApexPost.Infrastructure.Persistence;
using ApexPost.Infrastructure.Persistence.Repositories;
using ApexPost.Infrastructure.Persistence.SqlEntities;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using System.Text.Json;
using System.Data.Common;
using Xunit;

namespace ApexPost.Application.Tests.Persistence;

[CollectionDefinition("SQL Server Integration", DisableParallelization = true)]
public sealed class SqlServerIntegrationCollection : ICollectionFixture<SqlServerIntegrationTestFixture> { }

public sealed class SqlServerIntegrationTestFixture : IAsyncLifetime
{
    public string ConnectionString { get; private set; } = string.Empty;

    public async Task InitializeAsync()
    {
        var configured = Environment.GetEnvironmentVariable("APEXPOST_SQL_TEST_CONNECTION");
        if (string.IsNullOrWhiteSpace(configured))
        {
            return;
        }

        try
        {
            var builder = new SqlConnectionStringBuilder(configured);
            if (!builder.InitialCatalog.StartsWith("ApexPost_SqlIT_", StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException("SQL integration tests require a database named ApexPost_SqlIT_*; existing databases are not accepted.");
            }

            builder.InitialCatalog = $"ApexPost_SqlIT_{Guid.NewGuid():N}";
            ConnectionString = builder.ConnectionString;
            Environment.SetEnvironmentVariable("APEXPOST_SQL_TEST_CONNECTION", ConnectionString);

            await using (var master = new SqlConnection(new SqlConnectionStringBuilder(ConnectionString)
            {
                InitialCatalog = "master"
            }.ConnectionString))
            {
                await master.OpenAsync();
                await using var createDatabase = new SqlCommand(
                    $"CREATE DATABASE [{builder.InitialCatalog}]", master);
                await createDatabase.ExecuteNonQueryAsync();
            }

            await using (var context = new ApexPostDbContext(new DbContextOptionsBuilder<ApexPostDbContext>()
                .UseSqlServer(ConnectionString, sql => sql.EnableRetryOnFailure())
                .ConfigureWarnings(w => w.Ignore(RelationalEventId.PendingModelChangesWarning))
                .Options))
            {
                await context.Database.MigrateAsync();
            }
        }
        catch
        {
            await DropDatabaseIfExistsAsync(ConnectionString);
            throw;
        }
    }

    public async Task DisposeAsync()
    {
        await DropDatabaseIfExistsAsync(ConnectionString);
    }

    private static async Task DropDatabaseIfExistsAsync(string connectionString)
    {
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            return;
        }

        var builder = new SqlConnectionStringBuilder(connectionString);
        if (!builder.InitialCatalog.StartsWith("ApexPost_SqlIT_", StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        var databaseName = builder.InitialCatalog;
        builder.InitialCatalog = "master";
        await using var connection = new SqlConnection(builder.ConnectionString);
        await connection.OpenAsync();
        await using var dropDatabase = new SqlCommand(
            $@"IF DB_ID(@databaseName) IS NOT NULL
                BEGIN
                    ALTER DATABASE [{databaseName}] SET SINGLE_USER WITH ROLLBACK IMMEDIATE;
                    DROP DATABASE [{databaseName}];
                END;",
            connection);
        dropDatabase.Parameters.AddWithValue("@databaseName", databaseName);
        await dropDatabase.ExecuteNonQueryAsync();
    }
}

[Collection("SQL Server Integration")]
public sealed class SqlServerIntegrationTests
{
    private readonly SqlServerIntegrationTestFixture _fixture;
    private static readonly Lazy<Task> PrimarySchema = new(ApplyPrimaryMigrationsAsync);

    public SqlServerIntegrationTests(SqlServerIntegrationTestFixture fixture)
    {
        _fixture = fixture;
        Environment.SetEnvironmentVariable("APEXPOST_SQL_TEST_CONNECTION", fixture.ConnectionString);
    }

    [Fact]
    public void Address_IsNotPartOfTheCurrentSqlPersistenceModel()
    {
        using var context = new ApexPostDbContext(
            new DbContextOptionsBuilder<ApexPostDbContext>()
                .UseSqlServer("Server=(localdb)\\MSSQLLocalDB;Database=ApexPost_SqlIT_ModelOnly;Trusted_Connection=True;Encrypt=False")
                .Options);

        Assert.Null(context.Model.FindEntityType(typeof(Address)));
        Assert.DoesNotContain(
            context.Model.GetEntityTypes().SelectMany(entity => entity.GetProperties()),
            property => property.ClrType == typeof(Address));
    }

    [SqlServerFact]
    public async Task Migrations_CreateTheExpectedSchemaOnSqlServer()
    {
        await using var context = await CreateContextAsync();
        var applied = await context.Database.GetAppliedMigrationsAsync();
        Assert.Equal(10, applied.Count());
        Assert.Contains("20261103000000_HardenReportingProcedures", applied);
        Assert.Contains("20261103125806_PendingModelChanges", applied);
        Assert.Contains("20261104092655_FixLowStockThreshold", applied);
        Assert.Contains("20261107153525_PendingModelChanges2", applied);
        Assert.True(await context.Database.CanConnectAsync());
    }

    [SqlServerFact]
    public async Task Repositories_CreateUpdateAndReadThroughTheSharedScopedContext()
    {
        await using var provider = CreateProvider();
        var branch = NewBranch();
        using (var scope = provider.CreateScope())
        {
            var repo = scope.ServiceProvider.GetRequiredService<IBranchRepository>();
            var unit = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();
            await repo.AddAsync(branch);
            await unit.SaveChangesAsync();
            branch.Deactivate();
            await repo.UpdateAsync(branch);
            await unit.SaveChangesAsync();
            Assert.Same(
                scope.ServiceProvider.GetRequiredService<ApexPostDbContext>(),
                scope.ServiceProvider.GetRequiredService<ApexPostDbContext>());
        }

        using var verifyScope = provider.CreateScope();
        var stored = await verifyScope.ServiceProvider.GetRequiredService<IBranchRepository>()
            .GetByCodeAsync(branch.Code);
        Assert.NotNull(stored);
        Assert.False(stored.IsActive);
    }

    [SqlServerFact]
    public async Task TransactionRepository_LoadsCollectionsWithBoundedSplitQueryCommands()
    {
        await using var provider = CreateProvider();
        var seed = await SeedSaleAsync(provider, stockQuantity: 3);
        for (var index = 0; index < 4; index++)
        {
            await SeedPaidTransactionAsync(provider, seed);
        }

        var commandCounter = new ReaderCommandCounter();
        await using var context = new ApexPostDbContext(
            new DbContextOptionsBuilder<ApexPostDbContext>()
                .UseSqlServer(GetConnectionString(), sql => sql.EnableRetryOnFailure())
                .AddInterceptors(commandCounter)
                .Options);

        var transactions = await new SqlCounterTransactionRepository(context)
            .GetByBranchAsync(seed.BranchCode);

        Assert.Equal(4, transactions.Count);
        Assert.Equal(3, commandCounter.Count);
    }

    [SqlServerFact]
    public async Task UnitOfWork_CommitsAndRollsBackOnSqlServer()
    {
        await using var provider = CreateProvider();
        var committed = NewBranch();
        var rolledBack = NewBranch();
        using (var scope = provider.CreateScope())
        {
            var context = scope.ServiceProvider.GetRequiredService<ApexPostDbContext>();
            var unit = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();
            await unit.ExecuteInTransactionAsync(token =>
            {
                context.Branches.Add(committed);
                return Task.FromResult(true);
            });
            await Assert.ThrowsAsync<InvalidOperationException>(() => unit.ExecuteInTransactionAsync<bool>(token =>
            {
                context.Branches.Add(rolledBack);
                return Task.FromException<bool>(new InvalidOperationException("test rollback"));
            }));
        }

        await using var verify = await CreateContextAsync();
        Assert.True(await verify.Branches.AnyAsync(item => item.Code == committed.Code));
        Assert.False(await verify.Branches.AnyAsync(item => item.Code == rolledBack.Code));
    }

    [SqlServerFact]
    public async Task UnitOfWork_TranslatesUniqueKeyViolationToApplicationConflict()
    {
        var branch = NewBranch();
        await using (var setup = await CreateContextAsync())
        {
            setup.Branches.Add(branch);
            await setup.SaveChangesAsync();
        }

        await using var context = await CreateContextAsync();
        context.Branches.Add(branch);
        var unit = new SqlUnitOfWork(context);

        await Assert.ThrowsAsync<UniqueKeyConflictException>(() => unit.SaveChangesAsync());
    }

    [SqlServerFact]
    public async Task CounterSalePersistsStockTransactionLinesAndPaymentUsingDatabasePrice()
    {
        await using var provider = CreateProvider();
        var seed = await SeedSaleAsync(provider, stockQuantity: 6);
        using (var scope = provider.CreateScope())
        {
            var service = scope.ServiceProvider.GetRequiredService<CounterSaleService>();
            var result = await service.CompleteAsync(
                new CounterSaleRequest(seed.CounterId,
                    [new CounterSaleProductRequest(seed.Sku, 2)], PaymentMethod.Cash),
                new AuthenticatedStaff(seed.StaffId, StaffRole.BranchManager, seed.BranchCode));
            Assert.Equal(CounterSaleStatus.Completed, result.Status);
            Assert.NotNull(result.Transaction);
            Assert.Equal(20m, result.Transaction.Subtotal.Amount);
            Assert.Equal(PaymentMethod.Cash, result.Transaction.PaymentMethod);
        }

        await using var verify = await CreateContextAsync();
        Assert.Equal(4, (await verify.BranchStock.SingleAsync(item => item.BranchCode == new BranchCode(seed.BranchCode))).Quantity);
        var transaction = await verify.CounterTransactions.Include(item => item.Lines).Include(item => item.Payment)
            .SingleAsync(item => item.BranchCode == new BranchCode(seed.BranchCode));
        Assert.Single(transaction.Lines);
        Assert.NotNull(transaction.Payment);
        Assert.Null(typeof(CounterSaleProductRequest).GetProperty("UnitPrice"));
    }

    [SqlServerFact]
    public async Task SqlBusinessFlows_PersistShipmentMoneyOrderIdentityAndReportRows()
    {
        await using var provider = CreateProvider();
        var seed = await SeedSaleAsync(provider, stockQuantity: 8);
        string transactionId;
        string shipmentId;
        using (var scope = provider.CreateScope())
        {
            var result = await scope.ServiceProvider.GetRequiredService<CounterSaleService>().CompleteAsync(
                new CounterSaleRequest(seed.CounterId,
                    [new CounterSaleProductRequest(seed.Sku, 1)], PaymentMethod.Card,
                    new CounterShippingRequest("Parcel", 1m, new Dimensions(10m, 10m, 10m), "ZONE1", ShippingServiceLevel.Standard),
                    new CounterMoneyOrderRequest(15_000m, "Sender", "Recipient",
                        new CounterIdentityVerificationRequest("Government ID", "XXXX1234", true))),
                new AuthenticatedStaff(seed.StaffId, StaffRole.BranchManager, seed.BranchCode));
            Assert.Equal(CounterSaleStatus.Completed, result.Status);
            transactionId = result.Transaction!.Id;
            shipmentId = result.ShipmentId!;
            Assert.Contains(result.Transaction.LineItems,
                line => line.ItemType == "Service" && line.ReferenceId == shipmentId);
        }

        using (var scope = provider.CreateScope())
        {
            var reports = scope.ServiceProvider.GetRequiredService<IReportDataSource>();
            Assert.Contains(await reports.GetTransactionsAsync(), row => row.TransactionId == transactionId);
            Assert.Contains(await reports.GetShipmentsAsync(), row => row.BranchCode == seed.BranchCode);
            Assert.Contains(await reports.GetMoneyOrdersAsync(), row => row.BranchCode == seed.BranchCode);
            Assert.Contains(await reports.GetIdentityVerificationsAsync(), row => row.BranchCode == seed.BranchCode);
            var reportDate = DateOnly.FromDateTime(DateTime.UtcNow);
            var topProducts = await reports.GetTopProductsByRegionAsync(
                reportDate, reportDate, seed.BranchCode);
            var topProduct = Assert.Single(topProducts);
            Assert.Equal(seed.Sku, topProduct.Sku, ignoreCase: true);
            Assert.Equal(1, topProduct.RegionRank);
            var moneyOrderPage = await reports.GetMoneyOrderPageAsync(
                seed.BranchCode, reportDate, reportDate, pageNumber: 1, pageSize: 1);
            Assert.Equal(1, moneyOrderPage.TotalCount);
            Assert.Equal(15_000m, Assert.Single(moneyOrderPage.Items).Amount);
            Assert.Empty((await reports.GetMoneyOrderPageAsync(
                seed.BranchCode, null, null, pageNumber: 2, pageSize: 1)).Items);
            var verificationPage = await reports.GetIdentityVerificationPageAsync(
                seed.BranchCode, reportDate, reportDate, isVerified: true, pageNumber: 1, pageSize: 10);
            var verification = Assert.Single(verificationPage.Items);
            Assert.Equal("XXXX1234", verification.MaskedDocumentNumber);
            Assert.True(verification.IsVerified);
            var shipment = await scope.ServiceProvider.GetRequiredService<IShipmentRepository>().GetByIdAsync(shipmentId);
            Assert.Equal(transactionId, shipment!.CounterTransactionId);
            var moneyOrder = Assert.Single(await scope.ServiceProvider.GetRequiredService<IMoneyOrderRepository>()
                .GetByBranchAsync(seed.BranchCode));
            Assert.Equal("XXXX1234", moneyOrder.IdentityVerification!.MaskedDocumentNumber);
        }
    }

    [SqlServerFact]
    public async Task TransactionAndShipmentReportPages_FilterAndPaginateInSql()
    {
        await using var provider = CreateProvider();
        var branchSeed = await SeedSaleAsync(provider, stockQuantity: 8);
        var otherBranchSeed = await SeedSaleAsync(provider, stockQuantity: 8);
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var todayNoon = new DateTimeOffset(today.ToDateTime(new TimeOnly(12, 0), DateTimeKind.Utc));
        var yesterdayNoon = todayNoon.AddDays(-1);

        await SeedPaidTransactionAsync(provider, branchSeed, todayNoon);
        await SeedPaidTransactionAsync(provider, branchSeed, todayNoon.AddHours(1));
        await SeedPaidTransactionAsync(provider, branchSeed, yesterdayNoon);
        await SeedPaidTransactionAsync(provider, otherBranchSeed, todayNoon);

        await SeedShipmentAsync(provider, branchSeed.BranchCode, "SHP-PAGE-1", todayNoon);
        await SeedShipmentAsync(provider, branchSeed.BranchCode, "SHP-PAGE-2", todayNoon.AddHours(1));
        await SeedShipmentAsync(provider, branchSeed.BranchCode, "SHP-PAGE-3", yesterdayNoon);
        await SeedShipmentAsync(provider, otherBranchSeed.BranchCode, "SHP-PAGE-4", todayNoon);

        var commandCounter = new ReaderCommandCounter();
        await using var context = new ApexPostDbContext(
            new DbContextOptionsBuilder<ApexPostDbContext>()
                .UseSqlServer(GetConnectionString(), sql => sql.EnableRetryOnFailure())
                .AddInterceptors(commandCounter)
                .Options);
        var reports = new ReportingService(new SqlReportDataSource(context));

        var transactionPage1 = await reports.GetTransactionReportPageAsync(
            branchSeed.BranchCode, today, today, pageNumber: 1, pageSize: 1);
        var transactionPage2 = await reports.GetTransactionReportPageAsync(
            branchSeed.BranchCode, today, today, pageNumber: 2, pageSize: 1);
        var transactionPage3 = await reports.GetTransactionReportPageAsync(
            branchSeed.BranchCode, today, today, pageNumber: 3, pageSize: 1);
        Assert.Equal(2, transactionPage1.TotalCount);
        Assert.Equal(2, transactionPage2.TotalCount);
        Assert.Single(transactionPage1.Items);
        Assert.Single(transactionPage2.Items);
        Assert.Empty(transactionPage3.Items);
        Assert.Equal(2, transactionPage3.TotalCount);
        Assert.NotEqual(
            transactionPage1.Items.Single().OccurredAt,
            transactionPage2.Items.Single().OccurredAt);
        Assert.All(transactionPage1.Items.Concat(transactionPage2.Items),
            item => Assert.Equal(branchSeed.BranchCode, item.BranchCode));

        var shipmentPage1 = await reports.GetShipmentReportPageAsync(
            branchSeed.BranchCode, today, today, pageNumber: 1, pageSize: 1);
        var shipmentPage2 = await reports.GetShipmentReportPageAsync(
            branchSeed.BranchCode, today, today, pageNumber: 2, pageSize: 1);
        var shipmentPage3 = await reports.GetShipmentReportPageAsync(
            branchSeed.BranchCode, today, today, pageNumber: 3, pageSize: 1);
        Assert.Equal(2, shipmentPage1.TotalCount);
        Assert.Equal(2, shipmentPage2.TotalCount);
        Assert.Single(shipmentPage1.Items);
        Assert.Single(shipmentPage2.Items);
        Assert.Empty(shipmentPage3.Items);
        Assert.Equal(2, shipmentPage3.TotalCount);
        Assert.NotEqual(
            shipmentPage1.Items.Single().CreatedAt,
            shipmentPage2.Items.Single().CreatedAt);
        Assert.All(shipmentPage1.Items.Concat(shipmentPage2.Items),
            item => Assert.Equal(branchSeed.BranchCode, item.BranchCode));

        Assert.Contains(commandCounter.CommandTexts,
            command => command.Contains("OFFSET", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(commandCounter.CommandTexts,
            command => command.Contains("COUNT", StringComparison.OrdinalIgnoreCase));
    }

    [SqlServerFact]
    public async Task SqlVoidAndRefund_WorkflowsRestoreStockAndPersistTransitions()
    {
        await using var provider = CreateProvider();
        var voidSeed = await SeedSaleAsync(provider, stockQuantity: 0);
        var refundSeed = await SeedSaleAsync(provider, stockQuantity: 0);
        var voidId = await SeedPaidTransactionAsync(provider, voidSeed);
        var refundId = await SeedPaidTransactionAsync(provider, refundSeed);
        using (var scope = provider.CreateScope())
        {
            var voids = scope.ServiceProvider.GetRequiredService<TransactionVoidService>();
            var result = await voids.VoidAsync(voidId, "SQL integration void", voidSeed.StaffId);
            Assert.Equal(TransactionVoidStatus.Voided, result.Status);
        }
        using (var scope = provider.CreateScope())
        {
            var refunds = scope.ServiceProvider.GetRequiredService<TransactionRefundService>();
            var result = await refunds.RefundAsync(refundId, "SQL integration refund", refundSeed.StaffId);
            Assert.Equal(TransactionRefundStatus.Refunded, result.Status);
        }

        await using var verify = await CreateContextAsync();
        Assert.Equal(TransactionStatus.Voided,
            (await verify.CounterTransactions.SingleAsync(item => item.Id == voidId)).Status);
        Assert.Equal(TransactionStatus.Refunded,
            (await verify.CounterTransactions.SingleAsync(item => item.Id == refundId)).Status);
        Assert.Equal(2, (await verify.BranchStock.SingleAsync(item => item.BranchCode == new BranchCode(voidSeed.BranchCode))).Quantity);
        Assert.Equal(2, (await verify.BranchStock.SingleAsync(item => item.BranchCode == new BranchCode(refundSeed.BranchCode))).Quantity);
    }

    [SqlServerFact]
    public async Task FailedCounterSaleRestoresReservedStockAndLeavesNoPartialSale()
    {
        await using var provider = CreateProvider(failTransactionWrite: true);
        var seed = await SeedSaleAsync(provider, stockQuantity: 8);
        using (var scope = provider.CreateScope())
        {
            var result = await scope.ServiceProvider.GetRequiredService<CounterSaleService>().CompleteAsync(
                new CounterSaleRequest(seed.CounterId,
                    [new CounterSaleProductRequest(seed.Sku, 2)],
                    PaymentMethod.Cash,
                    new CounterShippingRequest(
                        "Parcel", 1m, new Dimensions(10m, 10m, 10m), "ZONE1", ShippingServiceLevel.Standard),
                    new CounterMoneyOrderRequest(500m, "Sender", "Recipient")),
                new AuthenticatedStaff(seed.StaffId, StaffRole.BranchManager, seed.BranchCode));
            Assert.Equal(CounterSaleStatus.PersistenceFailure, result.Status);
        }

        await using var verify = await CreateContextAsync();
        Assert.Equal(8, (await verify.BranchStock.SingleAsync(item => item.Sku == new Sku(seed.Sku))).Quantity);
        Assert.False(await verify.CounterTransactions.AnyAsync(item => item.BranchCode == new BranchCode(seed.BranchCode)));
        Assert.Empty(await verify.MoneyOrders.Where(item => item.BranchCode == new BranchCode(seed.BranchCode)).ToArrayAsync());
        Assert.Empty(await verify.Shipments.Where(item => item.BranchCode == new BranchCode(seed.BranchCode)).ToArrayAsync());
    }

    [SqlServerFact]
    public async Task SeparateContexts_RejectStaleConcurrentStockUpdate()
    {
        await using var provider = CreateProvider();
        var seed = await SeedSaleAsync(provider, stockQuantity: 8);
        await using var first = await CreateContextAsync();
        await using var second = await CreateContextAsync();
        var keyBranch = new BranchCode(seed.BranchCode);
        var keySku = new Sku(seed.Sku);
        var firstStock = await first.BranchStock.SingleAsync(item => item.BranchCode == keyBranch && item.Sku == keySku);
        var secondStock = await second.BranchStock.SingleAsync(item => item.BranchCode == keyBranch && item.Sku == keySku);
        firstStock.RemoveStock(1);
        secondStock.RemoveStock(1);
        await first.SaveChangesAsync();
        await Assert.ThrowsAsync<DbUpdateConcurrencyException>(() => second.SaveChangesAsync());
    }

    [SqlServerFact]
    public async Task ConcurrentSalesOfLastUnit_CommitExactlyOnePaidSale()
    {
        var gate = new SaleReservationGate(2);
        await using var provider = CreateProvider(saleGate: gate);
        var seed = await SeedSaleAsync(provider, stockQuantity: 1);

        async Task<CounterSaleResult> SellAsync()
        {
            using var scope = provider.CreateScope();
            return await scope.ServiceProvider.GetRequiredService<CounterSaleService>().CompleteAsync(
                new CounterSaleRequest(seed.CounterId,
                    [new CounterSaleProductRequest(seed.Sku, 1)], PaymentMethod.Cash),
                new AuthenticatedStaff(seed.StaffId, StaffRole.BranchManager, seed.BranchCode));
        }

        var outcomes = await Task.WhenAll(SellAsync(), SellAsync());

        Assert.Equal(1, outcomes.Count(result => result.Status == CounterSaleStatus.Completed));
        Assert.Equal(1, outcomes.Count(result =>
            result.Status is CounterSaleStatus.InsufficientStock or CounterSaleStatus.ConcurrencyConflict));
        await using var verify = await CreateContextAsync();
        Assert.Equal(0, (await verify.BranchStock.SingleAsync(item => item.BranchCode == new BranchCode(seed.BranchCode) && item.Sku == new Sku(seed.Sku))).Quantity);
        var transactions = await verify.CounterTransactions.Where(item => item.BranchCode == new BranchCode(seed.BranchCode)).ToArrayAsync();
        Assert.Single(transactions);
        Assert.Equal(TransactionStatus.Paid, transactions[0].Status);
        Assert.Equal(1, await verify.TransactionLines.CountAsync(item => item.TransactionId == transactions[0].Id));
        Assert.Equal(1, await verify.Payments.CountAsync(item => item.TransactionId == transactions[0].Id));
    }

    [SqlServerFact]
    public async Task ReportingProcedures_AreUsedForSeparatedRetailAndServiceTotals()
    {
        await using var provider = CreateProvider();
        var seed = await SeedSaleAsync(provider, stockQuantity: 6);
        using (var scope = provider.CreateScope())
        {
            var sale = await scope.ServiceProvider.GetRequiredService<CounterSaleService>().CompleteAsync(
                new CounterSaleRequest(seed.CounterId,
                    [new CounterSaleProductRequest(seed.Sku, 1)], PaymentMethod.Cash,
                    new CounterShippingRequest("Parcel", 1m, new Dimensions(10m, 10m, 10m), "ZONE1", ShippingServiceLevel.Standard)),
                new AuthenticatedStaff(seed.StaffId, StaffRole.BranchManager, seed.BranchCode));
            Assert.Equal(CounterSaleStatus.Completed, sale.Status);
        }

        using var reportScope = provider.CreateScope();
        var reports = reportScope.ServiceProvider.GetRequiredService<IReportDataSource>();
        var day = DateOnly.FromDateTime(DateTime.UtcNow);
        var daily = Assert.Single(await reports.GetDailyBranchSalesAsync(day, seed.BranchCode));
        Assert.Equal(1, daily.TransactionCount);
        Assert.Equal(11.8m, daily.RetailSales);
        Assert.Equal(70.8m, daily.ServiceSales);
        var noSales = Assert.Single(await reports.GetDailyBranchSalesAsync(day.AddDays(-1), seed.BranchCode));
        Assert.Equal(0, noSales.TransactionCount);
        Assert.Equal(0m, noSales.RetailSales);
        Assert.Equal(0m, noSales.ServiceSales);
        await Assert.ThrowsAsync<SqlException>(() =>
            reports.GetDailyBranchSalesAsync(day, "MISSING"));
        var lowStock = await reports.GetLowStockAsync(seed.BranchCode, 5, CancellationToken.None);
        Assert.Equal(5, Assert.Single(lowStock).QuantityOnHand);
        Assert.Empty(await reports.GetLowStockAsync(seed.BranchCode, 4, CancellationToken.None));

        var shipping = Assert.Single(await reports.GetShippingVolumeAsync(
            day, seed.BranchCode, CancellationToken.None));
        Assert.Equal("ZONE1", shipping.DestinationZone);
        Assert.Equal(nameof(ShippingServiceLevel.Standard), shipping.ServiceLevel);
        Assert.Equal(1, shipping.ShipmentCount);

        var activity = Assert.Single(await reports.GetBranchServiceActivityAsync(
            day, seed.BranchCode, CancellationToken.None));
        Assert.Equal(0, activity.MoneyOrderCount);
        Assert.Equal(0, activity.IdentityVerificationCount);
    }

    [SqlServerFact]
    public async Task PriceRevision_UsesSetBasedUpdateAndLeavesOtherCategoriesUnchanged()
    {
        await using var provider = CreateProvider();
        var seed = await SeedSaleAsync(provider, stockQuantity: 3);
        var otherSku = $"Q{Guid.NewGuid():N}"[..20];
        await using (var setup = await CreateContextAsync())
        {
            setup.Products.Add(new Product(
                new Sku(otherSku),
                "SQL Test Stationery",
                ProductCategory.Stationery,
                new Money(4m),
                TaxCategory.Standard));
            await setup.SaveChangesAsync();
        }

        int expectedUpdated;
        await using (var countContext = await CreateContextAsync())
        {
            expectedUpdated = await countContext.Products.CountAsync(
                item => item.Category == ProductCategory.Packaging);
        }

        using (var scope = provider.CreateScope())
        {
            var service = scope.ServiceProvider.GetRequiredService<ProductPriceRevisionService>();
            var updated = await service.SetCategoryPriceAsync(
                nameof(ProductCategory.Packaging), 12.34m);
            Assert.Equal(expectedUpdated, updated);
        }

        await using var verify = await CreateContextAsync();
        Assert.Equal(12.34m, (await verify.Products.SingleAsync(
            item => item.Sku == new Sku(seed.Sku))).UnitPrice.Amount);
        Assert.Equal(4m, (await verify.Products.SingleAsync(
            item => item.Sku == new Sku(otherSku))).UnitPrice.Amount);
    }

    [SqlServerFact]
    public async Task StockAdjustmentProcedure_RollsBackStockMovementWhenAdjustmentWouldGoNegative()
    {
        await using var provider = CreateProvider();
        var seed = await SeedSaleAsync(provider, stockQuantity: 5);
        await using var context = await CreateContextAsync();
        var stock = new SqlInventoryRepository(context);

        await Assert.ThrowsAsync<SqlException>(() => stock.AdjustQuantityWithAuditAsync(
            seed.BranchCode,
            seed.Sku,
            -6,
            "negative stock test",
            seed.StaffId));

        await using var verify = await CreateContextAsync();
        Assert.Equal(5, (await verify.BranchStock.SingleAsync(
            item => item.BranchCode == new BranchCode(seed.BranchCode) &&
                    item.Sku == new Sku(seed.Sku))).Quantity);
        Assert.False(await verify.StockMovements.AnyAsync(
            item => item.BranchCode == new BranchCode(seed.BranchCode) &&
                    item.Sku == new Sku(seed.Sku) &&
                    item.MovementType == "Adjustment"));
    }

    [SqlServerFact]
    public async Task SeparateContexts_CannotRestoreReversalStockTwice()
    {
        await using var provider = CreateProvider();
        var seed = await SeedSaleAsync(provider, stockQuantity: 0);
        var transactionId = await SeedPaidTransactionAsync(provider, seed);
        await using var firstContext = await CreateContextAsync();
        await using var secondContext = await CreateContextAsync();
        var first = new SqlInventoryRepository(firstContext);
        var second = new SqlInventoryRepository(secondContext);
        var quantities = new Dictionary<string, int> { [seed.Sku] = 2 };
        await first.RestoreReversedRetailStockAsync(transactionId, seed.BranchCode, quantities, "test reversal", seed.StaffId);
        await second.RestoreReversedRetailStockAsync(transactionId, seed.BranchCode, quantities, "test reversal", seed.StaffId);

        var outcomes = await Task.WhenAll(
            SaveRestoreAsync(firstContext),
            SaveRestoreAsync(secondContext));
        Assert.Equal(1, outcomes.Count(success => success));

        await using var verify = await CreateContextAsync();
        Assert.Equal(2, (await verify.BranchStock.SingleAsync(item => item.Sku == new Sku(seed.Sku))).Quantity);
        Assert.Equal(1, await verify.StockMovements.CountAsync(item => item.TransactionId == transactionId && item.MovementType == "ReversalRestore"));
    }

    [SqlServerFact]
    public async Task AuditStore_PersistsSafeAuditRecord()
    {
        await using var ensureSchema = await CreateContextAsync();
        await using var provider = CreateProvider();
        var id = Guid.NewGuid().ToString("N");
        using (var scope = provider.CreateScope())
        {
            await scope.ServiceProvider.GetRequiredService<IAuditStore>().WriteAsync(new AuditActionRecord(
                DateTimeOffset.UtcNow, "staff-test", "BranchManager", "B001", "Transactions", "Void",
                new Dictionary<string, string?> { ["transactionId"] = id }, 200, 8, id));
        }

        await using var verify = await CreateContextAsync();
        var record = await verify.AuditEntries.SingleAsync(item => item.CorrelationId == id);
        Assert.Equal("Transactions", record.Controller);
        Assert.Contains(id, record.RouteValuesJson);
    }

    [SqlServerFact]
    public async Task StockAdjustmentProcedure_ReturnsUpdatedStockAndWritesMovement()
    {
        await using var provider = CreateProvider();
        var seed = await SeedSaleAsync(provider, stockQuantity: 5);
        using (var scope = provider.CreateScope())
        {
            var stock = scope.ServiceProvider.GetRequiredService<SqlInventoryRepository>();
            var changed = await stock.AdjustQuantityWithAuditAsync(seed.BranchCode, seed.Sku, 3, "integration test", seed.StaffId);
            Assert.Equal(8, changed!.Quantity);
        }

        await using var verify = await CreateContextAsync();
        var movement = await verify.StockMovements.SingleAsync(item => item.BranchCode == new BranchCode(seed.BranchCode));
        Assert.Equal(3, movement.QuantityDelta);
        Assert.Equal("integration test", movement.Reason);
        Assert.Equal(seed.StaffId, movement.ChangedBy);
    }

    [SqlServerFact]
    public async Task JsonImport_RejectsInvalidInputThenImportsRepeatably()
    {
        var connection = GetImportConnectionString();
        var directory = Path.Combine(Path.GetTempPath(), $"apexpost-sql-import-{Guid.NewGuid():N}");
        Directory.CreateDirectory(directory);
        try
        {
            var options = new DbContextOptionsBuilder<ApexPostDbContext>().UseSqlServer(connection).Options;
            await using (var context = new ApexPostDbContext(options))
                await context.Database.MigrateAsync();
            var factoryOptions = new DbContextOptionsBuilder<ApexPostDbContext>().UseSqlServer(connection).Options;
            var factory = new TestContextFactory(factoryOptions);
            var importer = new JsonDataImporter(factory);
            await File.WriteAllTextAsync(Path.Combine(directory, "audit.jsonl"), "{invalid json}");
            await Assert.ThrowsAsync<InvalidDataException>(() => importer.PreviewAsync(directory));
            File.Delete(Path.Combine(directory, "audit.jsonl"));

            var first = await importer.ImportAsync(directory);
            var repeated = await importer.ImportAsync(directory);
            Assert.False(first.AlreadyImported);
            Assert.True(repeated.AlreadyImported);
            Assert.Equal(0, first.ImportedCounts.Values.Sum());
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
            await DropDatabaseIfExistsAsync(connection);
        }
    }

    [SqlServerFact]
    public async Task JsonAuditImport_AddsToExistingDatabaseAndIsRepeatSafe()
    {
        var existingBranch = NewBranch();
        await using (var context = await CreateContextAsync())
        {
            context.Branches.Add(existingBranch);
            await context.SaveChangesAsync();
        }

        var directory = Path.Combine(Path.GetTempPath(), $"apexpost-audit-import-{Guid.NewGuid():N}");
        Directory.CreateDirectory(directory);
        var correlationId = Guid.NewGuid().ToString("N");
        try
        {
            var entry = new AuditActionRecord(
                DateTimeOffset.UtcNow,
                "sql-import-test",
                "BranchManager",
                existingBranch.Code.Value,
                "Transactions",
                "Void",
                new Dictionary<string, string?> { ["transactionId"] = $"T{Guid.NewGuid():N}" },
                200,
                8,
                correlationId);
            await File.WriteAllTextAsync(
                Path.Combine(directory, "audit.jsonl"),
                JsonSerializer.Serialize(entry));

            var importer = new JsonDataImporter(
                new TestContextFactory(new DbContextOptionsBuilder<ApexPostDbContext>()
                    .UseSqlServer(GetConnectionString())
                    .Options));
            var first = await importer.ImportAuditLogAsync(directory);
            var repeated = await importer.ImportAuditLogAsync(directory);

            Assert.False(first.AlreadyImported);
            Assert.Equal(1, first.ImportedCounts["auditEvents"]);
            Assert.True(repeated.AlreadyImported);
            await using var verify = await CreateContextAsync();
            Assert.True(await verify.Branches.AnyAsync(item => item.Code == existingBranch.Code));
            Assert.Equal(1, await verify.AuditEntries.CountAsync(item => item.CorrelationId == correlationId));
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    private static async Task DropDatabaseIfExistsAsync(string connectionString)
    {
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            return;
        }

        var builder = new SqlConnectionStringBuilder(connectionString);
        if (!builder.InitialCatalog.StartsWith("ApexPost_SqlIT_", StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        var databaseName = builder.InitialCatalog;
        builder.InitialCatalog = "master";
        await using var connection = new SqlConnection(builder.ConnectionString);
        await connection.OpenAsync();
        await using var dropDatabase = new SqlCommand(
            $@"IF DB_ID(@databaseName) IS NOT NULL
                BEGIN
                    ALTER DATABASE [{databaseName}] SET SINGLE_USER WITH ROLLBACK IMMEDIATE;
                    DROP DATABASE [{databaseName}];
                END;",
            connection);
        dropDatabase.Parameters.AddWithValue("@databaseName", databaseName);
        await dropDatabase.ExecuteNonQueryAsync();
    }

    private static async Task<bool> SaveRestoreAsync(ApexPostDbContext context)
    {
        try
        {
            await context.SaveChangesAsync();
            return true;
        }
        catch (DbUpdateException)
        {
            return false;
        }
    }

    private static async Task<SaleSeed> SeedSaleAsync(IServiceProvider provider, int stockQuantity)
    {
        var key = Guid.NewGuid().ToString("N");
        var branchCode = new BranchCode($"B{key[..8]}");
        var counterId = $"C{key[..10]}";
        var staffId = $"S{key[..10]}";
        var sku = $"P{key[..9]}";
        await using var context = await CreateContextAsync();
        context.Branches.Add(new Branch(branchCode, "SQL Test Branch", "Test", new TimeOnly(0), new TimeOnly(23, 59)));
        context.Counters.Add(new Counter(counterId, branchCode, "SQL Test Counter"));
        context.Staff.Add(new Staff(staffId, "SQL Test Clerk", $"{staffId}@example.invalid", StaffRole.BranchManager, branchCode));
        context.Products.Add(new Product(new Sku(sku), "SQL Test Product", ProductCategory.Packaging, new Money(10m), TaxCategory.Standard));
        context.BranchStock.Add(new InventoryStock(branchCode, new Sku(sku), stockQuantity));
        await context.SaveChangesAsync();
        return new SaleSeed(branchCode.Value, counterId, staffId, sku);
    }

    private static async Task<string> SeedPaidTransactionAsync(
        IServiceProvider provider,
        SaleSeed seed,
        DateTimeOffset? occurredAtUtc = null)
    {
        var transaction = new CounterTransaction($"T{Guid.NewGuid():N}", new BranchCode(seed.BranchCode), seed.CounterId,
            seed.StaffId, [new TaxRate(TaxCategory.Standard, 0.18m)], occurredAtUtc ?? DateTimeOffset.UtcNow);
        transaction.AddLineItem(new TransactionLineItem("SQL test item", "Retail", 2, new Money(10m), TaxCategory.Standard, seed.Sku));
        transaction.CompletePayment(PaymentMethod.Cash);
        using var scope = provider.CreateScope();
        await scope.ServiceProvider.GetRequiredService<ICounterTransactionRepository>().AddAsync(transaction);
        await scope.ServiceProvider.GetRequiredService<IUnitOfWork>().SaveChangesAsync();
        return transaction.Id;
    }

    private static async Task SeedShipmentAsync(
        IServiceProvider provider,
        string branchCode,
        string shipmentId,
        DateTimeOffset createdAtUtc)
    {
        await using var context = await CreateContextAsync();
        context.Shipments.Add(new Shipment(
            shipmentId, "Parcel", new Weight(1m), new Dimensions(10m, 10m, 10m), "ZONE1",
            ShippingServiceLevel.Standard, new BranchCode(branchCode), createdAtUtc: createdAtUtc));
        await context.SaveChangesAsync();
    }

    private static ServiceProvider CreateProvider(bool failTransactionWrite = false, SaleReservationGate? saleGate = null)
    {
        var services = new ServiceCollection();
        var config = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["ConnectionStrings:ApexPost"] = GetConnectionString()
        }).Build();
        services.AddApplication();
        services.AddSqlInfrastructure(config);
        services.AddSingleton(new TaxRate(TaxCategory.Standard, 0.18m));
        services.AddSingleton(new TaxRate(TaxCategory.Reduced, 0.05m));
        services.AddSingleton(new TaxRate(TaxCategory.ZeroRated, 0m));
        services.AddSingleton(new TaxRate(TaxCategory.Exempt, 0m));
        services.AddSingleton(new ShippingPricingRule("ZONE1", ShippingServiceLevel.Standard, 20m, new Money(40m)));
        if (saleGate is not null)
        {
            services.RemoveAll<IInventoryRepository>();
            services.AddScoped<IInventoryRepository>(provider => new GatedInventoryRepository(
                provider.GetRequiredService<SqlInventoryRepository>(), saleGate));
        }
        if (failTransactionWrite)
        {
            services.AddScoped<SqlCounterTransactionRepository>();
            services.AddScoped<FailingTransactionRepository>();
            services.RemoveAll<ICounterTransactionRepository>();
            services.AddScoped<ICounterTransactionRepository>(sp => sp.GetRequiredService<FailingTransactionRepository>());
        }
        return services.BuildServiceProvider(new ServiceProviderOptions { ValidateScopes = true });
    }

    private static async Task<ApexPostDbContext> CreateContextAsync()
    {
        await PrimarySchema.Value;
        var context = new ApexPostDbContext(new DbContextOptionsBuilder<ApexPostDbContext>()
            .UseSqlServer(GetConnectionString(), sql => sql.EnableRetryOnFailure())
            .ConfigureWarnings(w => w.Ignore(RelationalEventId.PendingModelChangesWarning))
            .Options);
        return context;
    }

    private static async Task ApplyPrimaryMigrationsAsync()
    {
        await using var context = new ApexPostDbContext(new DbContextOptionsBuilder<ApexPostDbContext>()
            .UseSqlServer(GetConnectionString(), sql => sql.EnableRetryOnFailure())
            .ConfigureWarnings(w => w.Ignore(RelationalEventId.PendingModelChangesWarning))
            .Options);
        await context.Database.MigrateAsync();
    }

    private static Branch NewBranch()
    {
        var key = Guid.NewGuid().ToString("N");
        return new Branch(new BranchCode($"B{key[..8]}"), "SQL Test Branch", "Test", new TimeOnly(0), new TimeOnly(23, 59));
    }

    private static string GetConnectionString()
    {
        var value = Environment.GetEnvironmentVariable("APEXPOST_SQL_TEST_CONNECTION");
        if (string.IsNullOrWhiteSpace(value)) throw new InvalidOperationException("Set APEXPOST_SQL_TEST_CONNECTION to an isolated SQL Server test database.");
        var builder = new SqlConnectionStringBuilder(value);
        if (!builder.InitialCatalog.StartsWith("ApexPost_SqlIT_", StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("SQL integration tests require a database named ApexPost_SqlIT_*; existing databases are not accepted.");
        return builder.ConnectionString;
    }

    private static string GetImportConnectionString()
    {
        var builder = new SqlConnectionStringBuilder(GetConnectionString());
        builder.InitialCatalog += $"_Import_{Guid.NewGuid():N}"[..16];
        return builder.ConnectionString;
    }

    private sealed record SaleSeed(string BranchCode, string CounterId, string StaffId, string Sku);

    private sealed class ReaderCommandCounter : DbCommandInterceptor
    {
        public int Count { get; private set; }
        public List<string> CommandTexts { get; } = [];

        public override ValueTask<InterceptionResult<DbDataReader>> ReaderExecutingAsync(
            DbCommand command,
            CommandEventData eventData,
            InterceptionResult<DbDataReader> result,
            CancellationToken cancellationToken = default)
        {
            Count++;
            CommandTexts.Add(command.CommandText);
            return ValueTask.FromResult(result);
        }
    }

    private sealed class SaleReservationGate(int participants)
    {
        private int _arrivals;
        private readonly TaskCompletionSource _ready = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public async Task WaitAsync(CancellationToken cancellationToken)
        {
            if (Interlocked.Increment(ref _arrivals) == participants) _ready.TrySetResult();
            await _ready.Task.WaitAsync(cancellationToken);
        }
    }

    private sealed class GatedInventoryRepository(SqlInventoryRepository inner, SaleReservationGate gate) : IInventoryRepository
    {
        public async Task<InventoryReservationResult> TryReserveAsync(string branchCode, IReadOnlyDictionary<string, int> quantities, CancellationToken cancellationToken = default)
        {
            await gate.WaitAsync(cancellationToken);
            return await inner.TryReserveAsync(branchCode, quantities, cancellationToken);
        }
        public Task RestoreReservationAsync(InventoryReservation reservation, CancellationToken cancellationToken = default) => inner.RestoreReservationAsync(reservation, cancellationToken);
        public Task RestoreReversedRetailStockAsync(string transactionId, string branchCode, IReadOnlyDictionary<string, int> quantities, CancellationToken cancellationToken = default) => inner.RestoreReversedRetailStockAsync(transactionId, branchCode, quantities, cancellationToken);
        public Task RestoreReversedRetailStockAsync(string transactionId, string branchCode, IReadOnlyDictionary<string, int> quantities, string reason, string changedBy, CancellationToken cancellationToken = default) => inner.RestoreReversedRetailStockAsync(transactionId, branchCode, quantities, reason, changedBy, cancellationToken);
        public Task CommitReservationAsync(InventoryReservation reservation, CancellationToken cancellationToken = default) => inner.CommitReservationAsync(reservation, cancellationToken);
        public Task<InventoryStock?> GetByBranchAndSkuAsync(string branchCode, string sku, CancellationToken cancellationToken = default) => inner.GetByBranchAndSkuAsync(branchCode, sku, cancellationToken);
        public Task<IReadOnlyList<InventoryStock>> GetByBranchAsync(string branchCode, CancellationToken cancellationToken = default) => inner.GetByBranchAsync(branchCode, cancellationToken);
        public Task AddAsync(InventoryStock stock, CancellationToken cancellationToken = default) => inner.AddAsync(stock, cancellationToken);
        public Task UpdateAsync(InventoryStock stock, CancellationToken cancellationToken = default) => inner.UpdateAsync(stock, cancellationToken);
    }

    private sealed class TestContextFactory(DbContextOptions<ApexPostDbContext> options) : IDbContextFactory<ApexPostDbContext>
    {
        public ApexPostDbContext CreateDbContext() => new(options);
        public Task<ApexPostDbContext> CreateDbContextAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult(new ApexPostDbContext(options));
    }

    private sealed class FailingTransactionRepository(SqlCounterTransactionRepository inner) : ICounterTransactionRepository
    {
        public Task<CounterTransaction?> GetByIdAsync(string id, CancellationToken cancellationToken = default) => inner.GetByIdAsync(id, cancellationToken);
        public Task<IReadOnlyList<CounterTransaction>> GetByBranchAsync(string branchCode, CancellationToken cancellationToken = default) => inner.GetByBranchAsync(branchCode, cancellationToken);
        public Task AddAsync(CounterTransaction transaction, CancellationToken cancellationToken = default) => Task.FromException(new InvalidOperationException("Injected persistence failure"));
        public Task UpdateAsync(CounterTransaction transaction, CancellationToken cancellationToken = default) => inner.UpdateAsync(transaction, cancellationToken);
    }

    private sealed class SqlServerFactAttribute : FactAttribute
    {
        public SqlServerFactAttribute()
        {
            if (string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("APEXPOST_SQL_TEST_CONNECTION")))
                Skip = "Set APEXPOST_SQL_TEST_CONNECTION to run the isolated SQL Server integration suite.";
        }
    }
}
