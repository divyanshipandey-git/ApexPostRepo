using ApexPost.Application.Abstractions.Repositories;
using ApexPost.Application.Services.Shipping;
using ApexPost.Application.Services.Staff;
using ApexPost.Application.Services.Transactions;
using ApexPost.Application.Services.MoneyOrders;
using ApexPost.Application.Services.Notifications;
using ApexPost.Application.Notifications;
using ApexPost.Domain.Entities;
using ApexPost.Domain.ValueObjects;
using ApexPost.Infrastructure.Persistence;
using ApexPost.Infrastructure.Persistence.Repositories;
using ApexPost.Application.Tests.Persistence;
using Xunit;
using StaffEntity = ApexPost.Domain.Entities.Staff;

namespace ApexPost.Application.Tests.Services.Transactions;

public sealed class CounterSaleServiceTests
{
    [Fact]
    public async Task CompleteAsync_CompletesMixedSaleWithServerPricesTaxAndStockReservation()
    {
        using var fixture = new CounterSaleFixture(stockQuantity: 5);
        await fixture.SeedAsync();

        var result = await fixture.CreateService().CompleteAsync(
            fixture.CreateRequest(quantity: 2, includeShipping: true),
            fixture.StaffIdentity);

        Assert.Equal(CounterSaleStatus.Completed, result.Status);
        Assert.NotNull(result.Transaction);
        Assert.Equal(TransactionStatus.Paid, result.Transaction.Status);
        Assert.Equal(PaymentMethod.Cash, result.Transaction.PaymentMethod);
        Assert.Equal(170m, result.Transaction.Subtotal.Amount);
        Assert.Equal(30.6m, result.Transaction.Tax.Amount);
        Assert.Equal(200.6m, result.Transaction.Total.Amount);
        Assert.Contains(result.Transaction.LineItems, item => item.ItemType == "Retail");
        var serviceLine = Assert.Single(result.Transaction.LineItems, item => item.ItemType == "Service");
        Assert.Equal(result.ShipmentId, serviceLine.ReferenceId);
        Assert.NotNull(await fixture.Inventory.GetByBranchAndSkuAsync("IND01", "ENV001"));
        Assert.Equal(3, (await fixture.Inventory.GetByBranchAndSkuAsync("IND01", "ENV001"))!.Quantity);
        var shipment = await fixture.Shipments.GetByIdAsync(result.ShipmentId!);
        Assert.NotNull(shipment);
        Assert.Equal("IND01", shipment.BranchCode!.Value);
        Assert.Equal(result.Transaction.Id, shipment.CounterTransactionId);
        Assert.Single(await fixture.Shipments.GetByBranchAsync("IND01"));
        Assert.NotNull(await fixture.Transactions.GetByIdAsync(result.Transaction.Id));
        Assert.Contains(fixture.Notifications.GetPublishedNotifications(), notification =>
            notification.Type == NotificationType.LowStock && notification.BranchCode == "IND01");
    }

    [Fact]
    public async Task CompleteAsync_RecordsHighValueMoneyOrderAndVerificationWithPaidTransaction()
    {
        using var fixture = new CounterSaleFixture();
        await fixture.SeedAsync();

        var result = await fixture.CreateService().CompleteAsync(
            new CounterSaleRequest(
                "C001",
                [],
                PaymentMethod.Cash,
                MoneyOrder: new CounterMoneyOrderRequest(
                    15_000m,
                    "Sender",
                    "Recipient",
                    new CounterIdentityVerificationRequest("Government ID", "XXXX1234", true))),
            fixture.StaffIdentity);

        Assert.Equal(CounterSaleStatus.Completed, result.Status);
        var transaction = Assert.IsType<CounterTransaction>(result.Transaction);
        Assert.Equal(TransactionStatus.Paid, transaction.Status);
        var serviceLine = Assert.Single(transaction.LineItems);
        Assert.Equal("Service", serviceLine.ItemType);
        Assert.Equal(15_010m, serviceLine.UnitPrice.Amount);

        var moneyOrder = Assert.Single(await fixture.MoneyOrders.GetByBranchAsync("IND01"));
        Assert.Equal("IND01", moneyOrder.BranchCode);
        Assert.Equal(transaction.Id, moneyOrder.TransactionId);
        Assert.Equal(15_000m, moneyOrder.Amount.Amount);
        Assert.Equal(10m, moneyOrder.Fee.Amount);
        Assert.Equal("XXXX1234", moneyOrder.IdentityVerification!.MaskedDocumentNumber);
        Assert.True(moneyOrder.IdentityVerification.IsVerified);
        Assert.Equal(transaction.Id, (await fixture.Transactions.GetByIdAsync(transaction.Id))!.Id);
        Assert.Contains(fixture.Notifications.GetPublishedNotifications(), notification =>
            notification.Type == NotificationType.HighValueMoneyOrder && notification.BranchCode == "IND01");
    }

    [Fact]
    public async Task CompleteAsync_RejectsHighValueMoneyOrderWithoutVerificationBeforePersistence()
    {
        using var fixture = new CounterSaleFixture();
        await fixture.SeedAsync();

        var result = await fixture.CreateService().CompleteAsync(
            new CounterSaleRequest(
                "C001",
                [],
                PaymentMethod.Cash,
                MoneyOrder: new CounterMoneyOrderRequest(15_000m, "Sender", "Recipient")),
            fixture.StaffIdentity);

        Assert.Equal(CounterSaleStatus.MoneyOrderVerificationRequired, result.Status);
        Assert.Empty(await fixture.MoneyOrders.GetByBranchAsync("IND01"));
        Assert.Empty(await fixture.Transactions.GetByBranchAsync("IND01"));
    }

    [Fact]
    public async Task CompleteAsync_CompensatesMoneyOrderAndStockWhenTransactionPersistenceFails()
    {
        using var fixture = new CounterSaleFixture(stockQuantity: 5);
        await fixture.SeedAsync();
        var service = fixture.CreateService(new FailingAddTransactionRepository(fixture.Transactions));

        var result = await service.CompleteAsync(
            new CounterSaleRequest(
                "C001",
                [new CounterSaleProductRequest("ENV001", 2)],
                PaymentMethod.Cash,
                MoneyOrder: new CounterMoneyOrderRequest(500m, "Sender", "Recipient")),
            fixture.StaffIdentity);

        Assert.Equal(CounterSaleStatus.PersistenceFailure, result.Status);
        Assert.Empty(await fixture.MoneyOrders.GetByBranchAsync("IND01"));
        Assert.Empty(await fixture.Transactions.GetByBranchAsync("IND01"));
        Assert.Equal(5, (await fixture.Inventory.GetByBranchAndSkuAsync("IND01", "ENV001"))!.Quantity);
    }

    [Fact]
    public async Task CompleteAsync_RejectsInsufficientStockWithoutPersistingTransaction()
    {
        using var fixture = new CounterSaleFixture(stockQuantity: 1);
        await fixture.SeedAsync();

        var result = await fixture.CreateService().CompleteAsync(
            fixture.CreateRequest(quantity: 2),
            fixture.StaffIdentity);

        Assert.Equal(CounterSaleStatus.InsufficientStock, result.Status);
        Assert.Empty(await fixture.Transactions.GetByBranchAsync("IND01"));
        Assert.Equal(1, (await fixture.Inventory.GetByBranchAndSkuAsync("IND01", "ENV001"))!.Quantity);
    }

    [Fact]
    public async Task CompleteAsync_RejectsMissingProductAndInvalidQuantity()
    {
        using var fixture = new CounterSaleFixture();
        await fixture.SeedAsync();

        var missingProduct = await fixture.CreateService().CompleteAsync(
            new CounterSaleRequest("C001", [new CounterSaleProductRequest("NO-SUCH-SKU", 1)], PaymentMethod.Cash),
            fixture.StaffIdentity);
        var invalidQuantity = await fixture.CreateService().CompleteAsync(
            new CounterSaleRequest("C001", [new CounterSaleProductRequest("ENV001", 0)], PaymentMethod.Cash),
            fixture.StaffIdentity);

        Assert.Equal(CounterSaleStatus.InvalidProduct, missingProduct.Status);
        Assert.Equal(CounterSaleStatus.InvalidRequest, invalidQuantity.Status);
        Assert.Empty(await fixture.Transactions.GetByBranchAsync("IND01"));
    }

    [Fact]
    public async Task CompleteAsync_RejectsInactiveStaffWrongBranchAndClosedBranch()
    {
        using var fixture = new CounterSaleFixture();
        await fixture.SeedAsync();
        var request = fixture.CreateRequest();
        var service = fixture.CreateService();

        var staff = await fixture.Staff.GetByIdAsync(fixture.StaffIdentity.Id);
        staff!.Deactivate();
        await fixture.Staff.UpdateAsync(staff);
        var inactive = await service.CompleteAsync(request, fixture.StaffIdentity);
        var inactiveOptions = await service.GetOptionsAsync(fixture.StaffIdentity);

        staff.Activate();
        await fixture.Staff.UpdateAsync(staff);
        var wrongBranch = await service.CompleteAsync(
            request,
            fixture.StaffIdentity with { BranchCode = "IND02" });

        var branch = await fixture.Branches.GetByCodeAsync(new BranchCode("IND01"));
        branch!.Deactivate();
        await fixture.Branches.UpdateAsync(branch);
        var closed = await service.CompleteAsync(request, fixture.StaffIdentity);

        Assert.Equal(CounterSaleStatus.StaffInactive, inactive.Status);
        Assert.Null(inactiveOptions);
        Assert.Equal(CounterSaleStatus.Forbidden, wrongBranch.Status);
        Assert.Equal(CounterSaleStatus.BranchClosed, closed.Status);
        Assert.Empty(await fixture.Transactions.GetByBranchAsync("IND01"));
        Assert.Equal(12, (await fixture.Inventory.GetByBranchAndSkuAsync("IND01", "ENV001"))!.Quantity);
    }

    [Fact]
    public async Task CompleteAsync_RejectsInactiveCounterAndParcelAboveConfiguredMaximum()
    {
        using var fixture = new CounterSaleFixture();
        await fixture.SeedAsync();
        var counter = await fixture.Counters.GetByIdAsync("C001");
        counter!.Deactivate();
        await fixture.Counters.UpdateAsync(counter);

        var inactiveCounter = await fixture.CreateService().CompleteAsync(
            fixture.CreateRequest(),
            fixture.StaffIdentity);

        counter.Activate();
        await fixture.Counters.UpdateAsync(counter);
        var overweightRequest = fixture.CreateRequest(includeShipping: true) with
        {
            Shipping = new CounterShippingRequest(
                "Parcel", 31m, 20m, 15m, 10m, "ZONE1", ShippingServiceLevel.Standard)
        };
        var overweight = await fixture.CreateService().CompleteAsync(overweightRequest, fixture.StaffIdentity);

        Assert.Equal(CounterSaleStatus.CounterInactive, inactiveCounter.Status);
        Assert.Equal(CounterSaleStatus.ShippingUnavailable, overweight.Status);
        Assert.Empty(await fixture.Transactions.GetByBranchAsync("IND01"));
        Assert.Equal(12, (await fixture.Inventory.GetByBranchAndSkuAsync("IND01", "ENV001"))!.Quantity);
    }

    [Fact]
    public async Task CompleteAsync_RestoresStockAndRemovesShipmentWhenTransactionWriteFails()
    {
        using var fixture = new CounterSaleFixture(stockQuantity: 5);
        await fixture.SeedAsync();
        var service = fixture.CreateService(new FailingAddTransactionRepository(fixture.Transactions));

        var result = await service.CompleteAsync(
            fixture.CreateRequest(quantity: 2, includeShipping: true),
            fixture.StaffIdentity);

        Assert.Equal(CounterSaleStatus.PersistenceFailure, result.Status);
        Assert.Equal(5, (await fixture.Inventory.GetByBranchAndSkuAsync("IND01", "ENV001"))!.Quantity);
        Assert.Empty(await fixture.Transactions.GetByBranchAsync("IND01"));
        var storedShipments = await new JsonFileStore<Shipment>(
            fixture.Options.GetFilePath("shipments.json"),
            shipment => shipment.Id).GetAllAsync();
        Assert.Empty(storedShipments);
    }

    [Fact]
    public async Task ConcurrentSalesCannotOversellTheSameStock()
    {
        using var fixture = new CounterSaleFixture(stockQuantity: 1);
        await fixture.SeedAsync();
        var service = fixture.CreateService();
        var request = fixture.CreateRequest(quantity: 1);

        var outcomes = await Task.WhenAll(
            service.CompleteAsync(request, fixture.StaffIdentity),
            service.CompleteAsync(request, fixture.StaffIdentity));

        Assert.Single(outcomes, outcome => outcome.Status == CounterSaleStatus.Completed);
        Assert.Single(outcomes, outcome => outcome.Status == CounterSaleStatus.InsufficientStock);
        Assert.Equal(0, (await fixture.Inventory.GetByBranchAndSkuAsync("IND01", "ENV001"))!.Quantity);
        Assert.Single(await fixture.Transactions.GetByBranchAsync("IND01"));
    }

    private sealed class CounterSaleFixture : IDisposable
    {
        private readonly JsonRepositoryTestDirectory _directory;
        private readonly JsonPersistenceOptions _options;
        private readonly int _stockQuantity;

        public CounterSaleFixture(int stockQuantity = 12)
        {
            _stockQuantity = stockQuantity;
            _directory = new JsonRepositoryTestDirectory();
            _options = _directory.CreateOptions();
            Branches = new JsonBranchRepository(_options.DataDirectory);
            Counters = new JsonCounterRepository(_options);
            Staff = new JsonStaffRepository(_options.DataDirectory);
            Products = new JsonProductRepository(_options.DataDirectory);
            Inventory = new JsonInventoryRepository(_options);
            Transactions = new JsonCounterTransactionRepository(_options);
            Shipments = new JsonShipmentRepository(_options);
            MoneyOrders = new JsonMoneyOrderRepository(_options);
            Notifications = new SimulatedNotificationService();
        }

        public JsonBranchRepository Branches { get; }
        public JsonCounterRepository Counters { get; }
        public JsonStaffRepository Staff { get; }
        public JsonProductRepository Products { get; }
        public JsonInventoryRepository Inventory { get; }
        public JsonCounterTransactionRepository Transactions { get; }
        public JsonShipmentRepository Shipments { get; }
        public JsonMoneyOrderRepository MoneyOrders { get; }
        public SimulatedNotificationService Notifications { get; }
        public JsonPersistenceOptions Options => _options;
        public AuthenticatedStaff StaffIdentity { get; } = new("clerk-sale-1", StaffRole.CounterClerk, "IND01");

        public async Task SeedAsync()
        {
            await Branches.AddAsync(new Branch(
                new BranchCode("IND01"), "Central", "South", TimeOnly.MinValue, TimeOnly.MaxValue));
            await Counters.AddAsync(new Counter("C001", new BranchCode("IND01"), "Counter 1"));
            await Staff.AddAsync(new StaffEntity(
                StaffIdentity.Id, "Test Clerk", "clerk-sale@example.test", StaffRole.CounterClerk, new BranchCode("IND01")));
            await Products.AddAsync(new Product(
                new Sku("ENV001"), "Envelope", ProductCategory.Packaging, new Money(50m), TaxCategory.Standard));
            await Inventory.AddAsync(new InventoryStock(new BranchCode("IND01"), new Sku("ENV001"), _stockQuantity));
        }

        public CounterSaleService CreateService(ICounterTransactionRepository? transactionRepository = null)
        {
            var shipping = new ShippingService(
                Shipments,
                [new ShippingPricingRule("ZONE1", ShippingServiceLevel.Standard, 20m, new Money(40m))]);
            return new CounterSaleService(
                transactionRepository ?? Transactions,
                Inventory,
                Products,
                Staff,
                Branches,
                Counters,
                shipping,
                Shipments,
                new MoneyOrderService(MoneyOrders, new MoneyOrderPolicy(new Money(10_000m))),
                new MoneyOrderSettings(new Money(10m), new Money(10_000m)),
                Notifications,
                [new TaxRate(TaxCategory.Standard, 0.18m), new TaxRate(TaxCategory.Exempt, 0m)],
                TimeProvider.System);
        }

        public CounterSaleRequest CreateRequest(int quantity = 1, bool includeShipping = false) => new(
            "C001",
            [new CounterSaleProductRequest("ENV001", quantity)],
            PaymentMethod.Cash,
            includeShipping
                ? new CounterShippingRequest("Parcel", 1.5m, 20m, 15m, 10m, "ZONE1", ShippingServiceLevel.Standard)
                : null);

        public void Dispose() => _directory.Dispose();
    }

    private sealed class FailingAddTransactionRepository(ICounterTransactionRepository inner)
        : ICounterTransactionRepository
    {
        public Task<CounterTransaction?> GetByIdAsync(string transactionId, CancellationToken cancellationToken = default) =>
            inner.GetByIdAsync(transactionId, cancellationToken);

        public Task<IReadOnlyList<CounterTransaction>> GetByBranchAsync(string branchCode, CancellationToken cancellationToken = default) =>
            inner.GetByBranchAsync(branchCode, cancellationToken);

        public Task AddAsync(CounterTransaction transaction, CancellationToken cancellationToken = default) =>
            Task.FromException(new IOException("Injected transaction persistence failure."));

        public Task UpdateAsync(CounterTransaction transaction, CancellationToken cancellationToken = default) =>
            inner.UpdateAsync(transaction, cancellationToken);
    }
}
