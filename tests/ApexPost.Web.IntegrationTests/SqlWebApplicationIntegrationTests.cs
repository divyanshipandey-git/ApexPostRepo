using System.Security.Cryptography;
using System.Net;
using System.Text.RegularExpressions;
using ApexPost.Domain.Entities;
using ApexPost.Domain.ValueObjects;
using ApexPost.Application.Abstractions.Persistence;
using ApexPost.Application.Abstractions.Repositories;
using ApexPost.Application.Abstractions.Security;
using ApexPost.Infrastructure.Persistence;
using ApexPost.Infrastructure.Persistence.SqlEntities;
using ApexPost.Infrastructure.Security;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace ApexPost.Web.IntegrationTests;

[CollectionDefinition("SQL Server Integration", DisableParallelization = true)]
public sealed class SqlWebIntegrationCollection : ICollectionFixture<SqlWebIntegrationTestFixture> { }

public sealed class SqlWebIntegrationTestFixture : IAsyncLifetime
{
    public string ConnectionString { get; private set; } = string.Empty;

    public Task InitializeAsync()
    {
        var configured = Environment.GetEnvironmentVariable("APEXPOST_SQL_TEST_CONNECTION");
        if (string.IsNullOrWhiteSpace(configured))
        {
            return Task.CompletedTask;
        }

        try
        {
            var builder = new SqlConnectionStringBuilder(configured);
            if (!builder.InitialCatalog.StartsWith("ApexPost_SqlIT_", StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException("SQL HTTP integration tests require an ApexPost_SqlIT_* database name.");
            }

            builder.InitialCatalog = $"ApexPost_SqlIT_Http_{Guid.NewGuid():N}"[..38];
            ConnectionString = builder.ConnectionString;
            Environment.SetEnvironmentVariable("APEXPOST_SQL_CONNECTION", ConnectionString);
            Environment.SetEnvironmentVariable("APEXPOST_SQL_TEST_CONNECTION", ConnectionString);
            return Task.CompletedTask;
        }
        catch (Exception exception)
        {
            return Task.FromException(new InvalidOperationException("SQL HTTP integration test fixture failed to initialize.", exception));
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
        var databaseName = builder.InitialCatalog;
        if (!databaseName.StartsWith("ApexPost_SqlIT_", StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        builder.InitialCatalog = "master";
        await using var connection = new SqlConnection(builder.ConnectionString);
        await connection.OpenAsync();
        await using var command = new SqlCommand(
            $@"IF DB_ID(@databaseName) IS NOT NULL
                BEGIN
                    ALTER DATABASE [{databaseName}] SET SINGLE_USER WITH ROLLBACK IMMEDIATE;
                    DROP DATABASE [{databaseName}];
                END;",
            connection);
        command.Parameters.AddWithValue("@databaseName", databaseName);
        await command.ExecuteNonQueryAsync();
    }
}

[Collection("SQL Server Integration")]
public sealed class SqlWebApplicationIntegrationTests : IAsyncLifetime
{
    private readonly SqlWebApplicationFactory _factory;

    public SqlWebApplicationIntegrationTests(SqlWebIntegrationTestFixture fixture)
    {
        _factory = new SqlWebApplicationFactory(fixture.ConnectionString);
    }

    public async Task InitializeAsync() => await _factory.InitializeAsync();
    public async Task DisposeAsync() => await _factory.DisposeAsync();

    [SqlWebFact]
    public async Task SqlBackedMvcSale_PersistsTransactionPaymentShipmentMoneyOrderIdentityAndAudit()
    {
        using var client = await CreateAuthenticatedClientAsync();
        var token = await GetAntiforgeryTokenAsync(client, "/Transactions/Create");
        using var sale = await client.PostAsync("/Transactions/Create", Form(token,
            ("CounterId", SqlWebApplicationFactory.CounterId),
            ("PaymentMethod", "Cash"),
            ("Products[0].Sku", SqlWebApplicationFactory.Sku),
            ("Products[0].Quantity", "2"),
            ("IncludeShipping", "true"),
            ("ItemType", "Parcel"),
            ("WeightKg", "1"),
            ("LengthCm", "10"),
            ("WidthCm", "10"),
            ("HeightCm", "10"),
            ("DestinationZone", "ZONE1"),
            ("ShippingServiceLevel", "Standard"),
            ("IncludeMoneyOrder", "true"),
            ("MoneyOrderAmount", "15000"),
            ("MoneyOrderSenderName", "Sender"),
            ("MoneyOrderRecipientName", "Recipient"),
            ("IdentityDocumentType", "Government ID"),
            ("IdentityMaskedDocumentNumber", "XXXX1234"),
            ("IdentityVerified", "true")));

        Assert.Equal(HttpStatusCode.Redirect, sale.StatusCode);
        var receiptLocation = Assert.IsType<Uri>(sale.Headers.Location);
        var transactionId = receiptLocation.OriginalString.Split('/', StringSplitOptions.RemoveEmptyEntries).Last();

        using var receipt = await client.GetAsync(receiptLocation);
        Assert.Equal(HttpStatusCode.OK, receipt.StatusCode);
        Assert.Contains("Transaction receipt", await receipt.Content.ReadAsStringAsync());

        await using var db = _factory.CreateContext();
        var transaction = await db.CounterTransactions.Include(item => item.Lines).Include(item => item.Payment)
            .SingleAsync(item => item.Id == transactionId);
        Assert.Equal(TransactionStatus.Paid, transaction.Status);
        Assert.Equal(2, transaction.Lines.Single(item => item.ItemType == "Retail").Quantity);
        Assert.NotNull(transaction.Payment);
        Assert.Equal(3, (await db.BranchStock.SingleAsync(item => item.BranchCode == new BranchCode(SqlWebApplicationFactory.BranchCode) && item.Sku == new Sku(SqlWebApplicationFactory.Sku))).Quantity);
        Assert.Single(await db.Shipments.Where(item => item.CounterTransactionId == transactionId).ToArrayAsync());
        var order = await db.MoneyOrders.Include(item => item.IdentityVerification).SingleAsync(item => item.TransactionId == transactionId);
        Assert.Equal("XXXX1234", order.IdentityVerification!.MaskedDocumentNumber);
        Assert.DoesNotContain("1234", order.IdentityVerification.MaskedDocumentNumber[..4]);
        Assert.Contains(await db.AuditEntries.ToArrayAsync(), item => item.Controller == "Transactions" && item.Action == "Create" && item.RouteValuesJson.Contains(transactionId));
    }

    [SqlWebFact]
    public async Task SqlBackedAuditorReports_FilterAndPaginateMoneyOrdersAndVerifications()
    {
        using var manager = await CreateAuthenticatedClientAsync();
        var token = await GetAntiforgeryTokenAsync(manager, "/Transactions/Create");
        var saleIds = new List<string>();
        for (var index = 0; index < 2; index++)
        {
            using var sale = await manager.PostAsync("/Transactions/Create", Form(token,
                ("CounterId", SqlWebApplicationFactory.CounterId),
                ("PaymentMethod", "Cash"),
                ("Products[0].Sku", SqlWebApplicationFactory.Sku),
                ("Products[0].Quantity", "1"),
                ("IncludeMoneyOrder", "true"),
                ("MoneyOrderAmount", "15000"),
                ("MoneyOrderSenderName", "Audit sender"),
                ("MoneyOrderRecipientName", "Audit recipient"),
                ("IdentityDocumentType", "Government ID"),
                ("IdentityMaskedDocumentNumber", "XXXX1234"),
                ("IdentityVerified", "true"),
                ("Id", "ATTACKER-ID"),
                ("TransactionId", "ATTACKER-TXN"),
                ("BranchCode", "OTHER"),
                ("Status", "Voided"),
                ("CreatedAtUtc", "2000-01-01T00:00:00Z"),
                ("ModifiedAtUtc", "2000-01-01T00:00:00Z"),
                ("CreatedBy", "ATTACKER"),
                ("ModifiedBy", "ATTACKER"),
                ("OccurredAtUtc", "2000-01-01T00:00:00Z"),
                ("PaymentAmount", "0.01"),
                ("Subtotal", "0.01"),
                ("Total", "0.01")));
            Assert.Equal(HttpStatusCode.Redirect, sale.StatusCode);
            saleIds.Add(Assert.IsType<Uri>(sale.Headers.Location)
                .OriginalString.Split('/', StringSplitOptions.RemoveEmptyEntries).Last());
        }

        await using var db = _factory.CreateContext();
        var transactions = await db.CounterTransactions
            .Include(transaction => transaction.Payment)
            .Where(transaction => saleIds.Contains(transaction.Id))
            .ToArrayAsync();
        Assert.Equal(2, transactions.Length);
        Assert.All(transactions, transaction =>
        {
            Assert.NotEqual("ATTACKER-ID", transaction.Id);
            Assert.NotEqual("ATTACKER-TXN", transaction.Id);
            Assert.Equal(TransactionStatus.Paid, transaction.Status);
            Assert.Equal(SqlWebApplicationFactory.BranchCode, transaction.BranchCode.Value);
            Assert.NotEqual(0.01m, transaction.Subtotal);
            Assert.NotEqual(0.01m, transaction.Tax);
            Assert.NotEqual(0.01m, transaction.Total);
            Assert.NotNull(transaction.Payment);
            Assert.NotEqual(0.01m, transaction.Payment!.Amount);
            Assert.NotEqual(
                new DateTime(2000, 1, 1),
                transaction.OccurredAtUtc!.Value.UtcDateTime);
            Assert.NotEqual(
                new DateTime(2000, 1, 1),
                db.Entry(transaction).Property<DateTime>("CreatedAtUtc").CurrentValue);
            Assert.NotEqual(
                "ATTACKER",
                db.Entry(transaction).Property<string?>("CreatedBy").CurrentValue);
            Assert.Null(db.Entry(transaction).Metadata.FindProperty("ModifiedBy"));
            Assert.NotEqual(
                new DateTime(2000, 1, 1),
                db.Entry(transaction).Property<DateTime?>("ModifiedAtUtc").CurrentValue);
        });
        var storedOrders = await db.MoneyOrders
            .Include(order => order.IdentityVerification)
            .Where(order => order.BranchCode == new BranchCode(SqlWebApplicationFactory.BranchCode))
            .OrderByDescending(order => order.CreatedAtUtc)
            .ThenBy(order => order.Id)
            .ToArrayAsync();
        Assert.Equal(2, storedOrders.Length);
        Assert.All(storedOrders, order =>
        {
            Assert.NotEqual("ATTACKER-ID", order.Id);
            Assert.NotEqual("ATTACKER-TXN", order.TransactionId);
            Assert.Equal(SqlWebApplicationFactory.BranchCode, order.BranchCode!.Value);
            Assert.NotEqual(new DateTime(2000, 1, 1), order.CreatedAtUtc!.Value.UtcDateTime);
            Assert.NotEqual(
                "ATTACKER",
                db.Entry(order).Property<string?>("CreatedBy").CurrentValue);
            Assert.Null(db.Entry(order).Metadata.FindProperty("ModifiedBy"));
            Assert.Equal("XXXX1234", order.IdentityVerification!.MaskedDocumentNumber);
        });

        using var auditor = await CreateAuthenticatedClientAsync(
            SqlWebApplicationFactory.AuditorEmail,
            SqlWebApplicationFactory.AuditorPassword);
        var date = DateOnly.FromDateTime(DateTime.UtcNow).ToString("yyyy-MM-dd");
        using var firstPage = await auditor.GetAsync(
            $"/Reports/MoneyOrders?branchCode={SqlWebApplicationFactory.BranchCode}&fromDate={date}&toDate={date}&pageSize=1");
        Assert.Equal(HttpStatusCode.OK, firstPage.StatusCode);
        var firstHtml = await firstPage.Content.ReadAsStringAsync();
        Assert.Contains("Page 1 of 2", firstHtml);
        Assert.Contains(storedOrders[0].Id, firstHtml);
        Assert.DoesNotContain(storedOrders[1].Id, firstHtml);

        using var secondPage = await auditor.GetAsync(
            $"/Reports/MoneyOrders?branchCode={SqlWebApplicationFactory.BranchCode}&fromDate={date}&toDate={date}&page=2&pageSize=1");
        var secondHtml = await secondPage.Content.ReadAsStringAsync();
        Assert.Equal(HttpStatusCode.OK, secondPage.StatusCode);
        Assert.Contains("Page 2 of 2", secondHtml);
        Assert.Contains(storedOrders[1].Id, secondHtml);

        using var identityPage = await auditor.GetAsync(
            $"/Reports/IdentityVerifications?branchCode={SqlWebApplicationFactory.BranchCode}&fromDate={date}&toDate={date}&isVerified=true&pageSize=1");
        var identityHtml = await identityPage.Content.ReadAsStringAsync();
        Assert.Equal(HttpStatusCode.OK, identityPage.StatusCode);
        Assert.Contains("XXXX1234", identityHtml);
        Assert.Contains("Page 1 of 2", identityHtml);

        using var topProducts = await auditor.GetAsync(
            $"/Reports/TopProducts?branchCode={SqlWebApplicationFactory.BranchCode}&fromDate={date}&toDate={date}");
        Assert.Equal(HttpStatusCode.OK, topProducts.StatusCode);
        Assert.Contains(SqlWebApplicationFactory.Sku, await topProducts.Content.ReadAsStringAsync());

        using var invalidPage = await auditor.GetAsync("/Reports/MoneyOrders?pageSize=101");
        Assert.Equal(HttpStatusCode.BadRequest, invalidPage.StatusCode);
        using var invalidDate = await auditor.GetAsync("/Reports/MoneyOrders?fromDate=not-a-date");
        Assert.Equal(HttpStatusCode.BadRequest, invalidDate.StatusCode);
        using var invalidRange = await auditor.GetAsync(
            $"/Reports/IdentityVerifications?fromDate={date}&toDate=2000-01-01");
        Assert.Equal(HttpStatusCode.BadRequest, invalidRange.StatusCode);

        using var managerReport = await manager.GetAsync("/Reports/MoneyOrders");
        Assert.Equal(HttpStatusCode.Redirect, managerReport.StatusCode);
    }

    [SqlWebFact]
    public async Task SqlBackedMvcVoid_UsesManagerAuthorizationRestoresStockAndWritesAudit()
    {
        var transactionId = await _factory.CreatePaidRetailTransactionAsync(quantity: 1);
        using var client = await CreateAuthenticatedClientAsync();
        var token = await GetAntiforgeryTokenAsync(client, $"/Transactions/Details/{transactionId}");
        using var response = await client.PostAsync("/Transactions/Void", Form(token,
            ("TransactionId", transactionId), ("VoidReason", "Customer correction")));

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        await using var db = _factory.CreateContext();
        Assert.Equal(TransactionStatus.Voided, (await db.CounterTransactions.SingleAsync(item => item.Id == transactionId)).Status);
        // The seed sale consumes one of the five on-hand units; a successful
        // reversal returns that same unit and restores the original quantity.
        Assert.Equal(5, (await db.BranchStock.SingleAsync(item => item.BranchCode == new BranchCode(SqlWebApplicationFactory.BranchCode) && item.Sku == new Sku(SqlWebApplicationFactory.Sku))).Quantity);
        Assert.Single(await db.StockMovements.Where(item => item.TransactionId == transactionId && item.MovementType == "ReversalRestore").ToArrayAsync());
        Assert.Contains(await db.AuditEntries.ToArrayAsync(), item => item.Action == "Void" && item.RouteValuesJson.Contains(transactionId));
    }

    [SqlWebFact]
    public async Task SqlBackedMvcLoginDashboardCatalogueInventoryAndReportsReadSqlData()
    {
        using var client = await CreateAuthenticatedClientAsync();
        foreach (var path in new[] { "/", "/Products", "/Inventory/Index", "/Stock/Index", "/Reports" })
        {
            using var response = await client.GetAsync(path);
            Assert.True(response.StatusCode == HttpStatusCode.OK,
                $"{path} returned {(int)response.StatusCode}: {await response.Content.ReadAsStringAsync()}");
        }

        await using var db = _factory.CreateContext();
        Assert.True(await db.Branches.AnyAsync(item => item.Code == new BranchCode(SqlWebApplicationFactory.BranchCode)));
        Assert.True(await db.Products.AnyAsync(item => item.Sku == new Sku(SqlWebApplicationFactory.Sku)));

        using var filteredReports = await client.GetAsync(
            $"/Reports?date={DateOnly.FromDateTime(DateTime.UtcNow):yyyy-MM-dd}&branchCode={SqlWebApplicationFactory.BranchCode}&threshold=0");
        Assert.Equal(HttpStatusCode.OK, filteredReports.StatusCode);
        var reportHtml = await filteredReports.Content.ReadAsStringAsync();
        Assert.Contains("name=\"branchCode\"", reportHtml, StringComparison.Ordinal);
        Assert.Contains("name=\"threshold\"", reportHtml, StringComparison.Ordinal);
        Assert.Contains("value=\"0\"", reportHtml, StringComparison.Ordinal);

        using var invalidThreshold = await client.GetAsync("/Reports?threshold=100001");
        Assert.Equal(HttpStatusCode.BadRequest, invalidThreshold.StatusCode);
    }

    [SqlWebFact]
    public async Task SqlBackedLoginEnforcesPermissionsForEverySupportedRole()
    {
        var accounts = new[]
        {
            (StaffId: SqlWebApplicationFactory.StaffId, Email: SqlWebApplicationFactory.Email, Password: SqlWebApplicationFactory.Password, Role: StaffRole.BranchManager),
            CreateRoleAccount("SQLHTTP-ADMIN", "sqlhttp.admin@example.test", StaffRole.Administrator),
            CreateRoleAccount("SQLHTTP-AUDITOR", "sqlhttp.auditor@example.test", StaffRole.Auditor),
            CreateRoleAccount("SQLHTTP-CLERK", "sqlhttp.clerk@example.test", StaffRole.CounterClerk)
        };

        await using (var db = _factory.CreateContext())
        {
            var hasher = new Pbkdf2StaffPasswordHasher();
            foreach (var account in accounts.Skip(1))
            {
                db.Staff.Add(new Staff(
                    account.StaffId,
                    $"SQL HTTP {account.Role}",
                    account.Email,
                    account.Role,
                    new BranchCode(SqlWebApplicationFactory.BranchCode)));
                db.StaffCredentials.Add(new StaffCredential(account.StaffId, hasher.HashPassword(account.Password)));
            }
            await db.SaveChangesAsync();
        }

        foreach (var account in accounts)
        {
            using var client = await CreateAuthenticatedClientAsync(account.Email, account.Password);
            var sale = await client.GetAsync("/Transactions/Create");
            var reports = await client.GetAsync("/Reports");
            var inventory = await client.GetAsync("/Inventory/Index");
            var priceRevision = await client.GetAsync("/Products/PriceRevision");
            var stock = await client.GetAsync("/Stock/Index?branchCode=SQLHTTP1");
            var otherBranchStock = await client.GetAsync("/Stock/Index?branchCode=OTHER");
            var canSell = account.Role is StaffRole.Administrator or StaffRole.BranchManager or StaffRole.CounterClerk;
            var canUseReports = account.Role is StaffRole.Administrator or StaffRole.BranchManager or StaffRole.Auditor;
            var canUseInventory = account.Role is StaffRole.Administrator or StaffRole.BranchManager or StaffRole.CounterClerk;
            var canRevisePrices = account.Role == StaffRole.Administrator;
            var canManageStock = account.Role is StaffRole.Administrator or StaffRole.BranchManager;

            await AssertStatusAsync(
                account.Role,
                "/Transactions/Create",
                canSell ? HttpStatusCode.OK : HttpStatusCode.Redirect,
                sale);
            await AssertStatusAsync(
                account.Role,
                "/Reports",
                canUseReports ? HttpStatusCode.OK : HttpStatusCode.Redirect,
                reports);
            await AssertStatusAsync(
                account.Role,
                "/Inventory/Index",
                canUseInventory ? HttpStatusCode.OK : HttpStatusCode.Redirect,
                inventory);
            await AssertStatusAsync(
                account.Role,
                "/Products/PriceRevision",
                canRevisePrices ? HttpStatusCode.OK : HttpStatusCode.Redirect,
                priceRevision);
            await AssertStatusAsync(
                account.Role,
                "/Stock/Index",
                canManageStock ? HttpStatusCode.OK : HttpStatusCode.Redirect,
                stock);
            await AssertStatusAsync(
                account.Role,
                "/Stock/Index?branchCode=OTHER",
                account.Role == StaffRole.Administrator ? HttpStatusCode.OK : HttpStatusCode.Redirect,
                otherBranchStock);
        }

        static async Task AssertStatusAsync(
            StaffRole role,
            string path,
            HttpStatusCode expected,
            HttpResponseMessage response)
        {
            Assert.True(
                response.StatusCode == expected,
                $"{role} request to {path} returned {(int)response.StatusCode} {response.StatusCode}: {await response.Content.ReadAsStringAsync()}");
        }

        static (string StaffId, string Email, string Password, StaffRole Role) CreateRoleAccount(
            string staffId,
            string email,
            StaffRole role) =>
            (staffId, email, Convert.ToBase64String(RandomNumberGenerator.GetBytes(24)), role);
    }

    private Task<HttpClient> CreateAuthenticatedClientAsync() =>
        CreateAuthenticatedClientAsync(SqlWebApplicationFactory.Email, SqlWebApplicationFactory.Password);

    private async Task<HttpClient> CreateAuthenticatedClientAsync(string email, string password)
    {
        var client = _factory.CreateClient(new WebApplicationFactoryClientOptions { BaseAddress = new Uri("https://localhost"), AllowAutoRedirect = false });
        var token = await GetAntiforgeryTokenAsync(client, "/Account/Login");
        using var login = await client.PostAsync("/Account/Login", Form(token,
            ("Email", email), ("Password", password)));
        Assert.Equal(HttpStatusCode.Redirect, login.StatusCode);
        return client;
    }

    private static FormUrlEncodedContent Form(string token, params (string Key, string Value)[] values) =>
        new([new KeyValuePair<string, string>("__RequestVerificationToken", token), .. values.Select(item => new KeyValuePair<string, string>(item.Key, item.Value))]);

    private static async Task<string> GetAntiforgeryTokenAsync(HttpClient client, string path)
    {
        var html = await client.GetStringAsync(path);
        var match = Regex.Match(html, "name=\"__RequestVerificationToken\" type=\"hidden\" value=\"(?<token>[^\"]+)\"");
        return match.Success ? WebUtility.HtmlDecode(match.Groups["token"].Value) : throw new InvalidOperationException("Antiforgery token was not rendered.");
    }

    private sealed class SqlWebFactAttribute : FactAttribute
    {
        public SqlWebFactAttribute()
        {
            if (string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("APEXPOST_SQL_TEST_CONNECTION")))
                Skip = "Set APEXPOST_SQL_TEST_CONNECTION to run SQL-backed HTTP integration tests.";
        }
    }
}

internal sealed class SqlWebApplicationFactory : WebApplicationFactory<Program>
{
    public const string BranchCode = "SQLHTTP1";
    public const string CounterId = "SQLHTTP-COUNTER";
    public const string StaffId = "SQLHTTP-MANAGER";
    public const string AuditorId = "SQLHTTP-REPORT-AUDITOR";
    public const string Email = "sqlhttp.manager@example.test";
    public const string AuditorEmail = "sqlhttp.report-auditor@example.test";
    public static readonly string Password =
        Convert.ToBase64String(RandomNumberGenerator.GetBytes(24));
    public static readonly string AuditorPassword =
        Convert.ToBase64String(RandomNumberGenerator.GetBytes(24));
    public const string Sku = "SQLHTTPSKU";
    private readonly string _connectionString;
    private bool _initialized;

    public SqlWebApplicationFactory(string? configuredConnectionString = null)
    {
        var configured = configuredConnectionString ?? Environment.GetEnvironmentVariable("APEXPOST_SQL_TEST_CONNECTION")
            ?? throw new InvalidOperationException("APEXPOST_SQL_TEST_CONNECTION is required.");
        var builder = new SqlConnectionStringBuilder(configured);
        if (!builder.InitialCatalog.StartsWith("ApexPost_SqlIT_", StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("SQL HTTP integration tests require an ApexPost_SqlIT_* database name.");
        builder.InitialCatalog = $"ApexPost_SqlIT_Http_{Guid.NewGuid():N}"[..38];
        _connectionString = builder.ConnectionString;
        Environment.SetEnvironmentVariable("APEXPOST_SQL_CONNECTION", _connectionString);
    }

    public async Task InitializeAsync()
    {
        if (_initialized) return;
        try
        {
            await using var connection = new SqlConnection(new SqlConnectionStringBuilder(_connectionString)
            {
                InitialCatalog = "master"
            }.ConnectionString);
            await connection.OpenAsync();
            await using var createDb = new SqlCommand($"CREATE DATABASE [{new SqlConnectionStringBuilder(_connectionString).InitialCatalog}]", connection);
            await createDb.ExecuteNonQueryAsync();

            using var scope = Services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<ApexPostDbContext>();
            await db.Database.MigrateAsync();
            var hasher = scope.ServiceProvider.GetRequiredService<IStaffPasswordHasher>();
            db.Branches.Add(new Branch(new BranchCode(BranchCode), "SQL HTTP Branch", "Test", new TimeOnly(0), new TimeOnly(23, 59)));
            db.Counters.Add(new Counter(CounterId, new BranchCode(BranchCode), "SQL HTTP Counter"));
            db.Staff.Add(new Staff(StaffId, "SQL HTTP Manager", Email, StaffRole.BranchManager, new BranchCode(BranchCode)));
            db.Staff.Add(new Staff(AuditorId, "SQL HTTP Auditor", AuditorEmail, StaffRole.Auditor, new BranchCode(BranchCode)));
            db.StaffCredentials.Add(new StaffCredential(StaffId, hasher.HashPassword(Password)));
            db.StaffCredentials.Add(new StaffCredential(AuditorId, hasher.HashPassword(AuditorPassword)));
            db.Products.Add(new Product(new Sku(Sku), "SQL HTTP Envelope", ProductCategory.Packaging, new Money(10m), TaxCategory.Standard));
            db.BranchStock.Add(new InventoryStock(new BranchCode(BranchCode), new Sku(Sku), 5));
            await db.SaveChangesAsync();
            _initialized = true;
        }
        catch
        {
            await DropDatabaseIfExistsAsync(_connectionString);
            throw;
        }
    }

    public new async Task DisposeAsync()
    {
        try
        {
            await base.DisposeAsync();
        }
        finally
        {
            await DropDatabaseIfExistsAsync(_connectionString);
        }
    }

    private static async Task DropDatabaseIfExistsAsync(string connectionString)
    {
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            return;
        }

        var builder = new SqlConnectionStringBuilder(connectionString);
        var databaseName = builder.InitialCatalog;
        if (!databaseName.StartsWith("ApexPost_SqlIT_", StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

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

    public ApexPostDbContext CreateContext() => new(new DbContextOptionsBuilder<ApexPostDbContext>().UseSqlServer(_connectionString).Options);

    public async Task<string> CreatePaidRetailTransactionAsync(int quantity)
    {
        var id = $"SQLHTTP-TX-{Guid.NewGuid():N}";
        var transaction = new CounterTransaction(id, new BranchCode(BranchCode), CounterId, StaffId,
            [new TaxRate(TaxCategory.Standard, .18m)], DateTimeOffset.UtcNow);
        transaction.AddLineItem(new TransactionLineItem("SQL HTTP Envelope", "Retail", quantity, new Money(10m), TaxCategory.Standard, Sku));
        transaction.CompletePayment(PaymentMethod.Cash);
        using var scope = Services.CreateScope();
        await scope.ServiceProvider.GetRequiredService<ICounterTransactionRepository>().AddAsync(transaction);
        var stock = await scope.ServiceProvider.GetRequiredService<IInventoryRepository>().GetByBranchAndSkuAsync(BranchCode, Sku)
            ?? throw new InvalidOperationException("Seed stock was not found.");
        stock.RemoveStock(quantity);
        await scope.ServiceProvider.GetRequiredService<IInventoryRepository>().UpdateAsync(stock);
        await scope.ServiceProvider.GetRequiredService<IUnitOfWork>().SaveChangesAsync();
        return id;
    }
}
