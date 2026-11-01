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

[CollectionDefinition("SQL Web Integration", DisableParallelization = true)]
public sealed class SqlWebIntegrationCollection { }

[Collection("SQL Web Integration")]
public sealed class SqlWebApplicationIntegrationTests : IAsyncLifetime
{
    private readonly SqlWebApplicationFactory _factory = new();

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
            var stock = await client.GetAsync("/Stock/Index?branchCode=SQLHTTP1");
            var otherBranchStock = await client.GetAsync("/Stock/Index?branchCode=OTHER");
            var canSell = account.Role is StaffRole.Administrator or StaffRole.BranchManager or StaffRole.CounterClerk;
            var canUseReports = account.Role is StaffRole.Administrator or StaffRole.BranchManager or StaffRole.Auditor;
            var canUseInventory = account.Role is StaffRole.Administrator or StaffRole.BranchManager or StaffRole.CounterClerk;
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
    public const string Email = "sqlhttp.manager@example.test";
    public static readonly string Password =
        Convert.ToBase64String(RandomNumberGenerator.GetBytes(24));
    public const string Sku = "SQLHTTPSKU";
    private readonly string _connectionString;
    private bool _initialized;

    public SqlWebApplicationFactory()
    {
        var configured = Environment.GetEnvironmentVariable("APEXPOST_SQL_TEST_CONNECTION")
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
        using var scope = Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApexPostDbContext>();
        await db.Database.MigrateAsync();
        var hasher = scope.ServiceProvider.GetRequiredService<IStaffPasswordHasher>();
        db.Branches.Add(new Branch(new BranchCode(BranchCode), "SQL HTTP Branch", "Test", new TimeOnly(0), new TimeOnly(23, 59)));
        db.Counters.Add(new Counter(CounterId, new BranchCode(BranchCode), "SQL HTTP Counter"));
        db.Staff.Add(new Staff(StaffId, "SQL HTTP Manager", Email, StaffRole.BranchManager, new BranchCode(BranchCode)));
        db.StaffCredentials.Add(new StaffCredential(StaffId, hasher.HashPassword(Password)));
        db.Products.Add(new Product(new Sku(Sku), "SQL HTTP Envelope", ProductCategory.Packaging, new Money(10m), TaxCategory.Standard));
        db.BranchStock.Add(new InventoryStock(new BranchCode(BranchCode), new Sku(Sku), 5));
        await db.SaveChangesAsync();
        _initialized = true;
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
