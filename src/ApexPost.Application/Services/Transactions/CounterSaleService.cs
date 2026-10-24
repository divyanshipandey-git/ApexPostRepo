using ApexPost.Application.Abstractions.Repositories;
using ApexPost.Application.Services.Staff;
using ApexPost.Application.Services.Shipping;
using ApexPost.Domain.Entities;
using ApexPost.Domain.ValueObjects;

namespace ApexPost.Application.Services.Transactions;

public sealed class CounterSaleService
{
    private readonly ICounterTransactionRepository _transactionRepository;
    private readonly IInventoryRepository _inventoryRepository;
    private readonly IProductRepository _productRepository;
    private readonly IStaffRepository _staffRepository;
    private readonly IBranchRepository _branchRepository;
    private readonly ICounterRepository _counterRepository;
    private readonly ShippingService _shippingService;
    private readonly IShipmentRepository _shipmentRepository;
    private readonly IReadOnlyCollection<TaxRate> _taxRates;
    private readonly TimeProvider _timeProvider;

    public CounterSaleService(
        ICounterTransactionRepository transactionRepository,
        IInventoryRepository inventoryRepository,
        IProductRepository productRepository,
        IStaffRepository staffRepository,
        IBranchRepository branchRepository,
        ICounterRepository counterRepository,
        ShippingService shippingService,
        IShipmentRepository shipmentRepository,
        IEnumerable<TaxRate> taxRates,
        TimeProvider timeProvider)
    {
        ArgumentNullException.ThrowIfNull(transactionRepository);
        ArgumentNullException.ThrowIfNull(inventoryRepository);
        ArgumentNullException.ThrowIfNull(productRepository);
        ArgumentNullException.ThrowIfNull(staffRepository);
        ArgumentNullException.ThrowIfNull(branchRepository);
        ArgumentNullException.ThrowIfNull(counterRepository);
        ArgumentNullException.ThrowIfNull(shippingService);
        ArgumentNullException.ThrowIfNull(shipmentRepository);
        ArgumentNullException.ThrowIfNull(taxRates);
        ArgumentNullException.ThrowIfNull(timeProvider);

        _transactionRepository = transactionRepository;
        _inventoryRepository = inventoryRepository;
        _productRepository = productRepository;
        _staffRepository = staffRepository;
        _branchRepository = branchRepository;
        _counterRepository = counterRepository;
        _shippingService = shippingService;
        _shipmentRepository = shipmentRepository;
        _taxRates = taxRates.ToArray();
        _timeProvider = timeProvider;
    }

    public async Task<CounterSaleResult> CompleteAsync(
        CounterSaleRequest request,
        AuthenticatedStaff authenticatedStaff,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(authenticatedStaff);
        if (!IsValidRequest(request))
        {
            return new CounterSaleResult(CounterSaleStatus.InvalidRequest);
        }

        var staff = await _staffRepository.GetByIdAsync(authenticatedStaff.Id, cancellationToken);
        if (staff is null)
        {
            return new CounterSaleResult(CounterSaleStatus.StaffNotFound);
        }

        if (!staff.IsActive)
        {
            return new CounterSaleResult(CounterSaleStatus.StaffInactive);
        }

        if (staff.Role != authenticatedStaff.Role ||
            staff.HomeBranchCode.Value != authenticatedStaff.BranchCode ||
            staff.Role is not (StaffRole.CounterClerk or StaffRole.BranchManager or StaffRole.Administrator))
        {
            return new CounterSaleResult(CounterSaleStatus.Forbidden);
        }

        BranchCode branchCode;
        try
        {
            branchCode = new BranchCode(authenticatedStaff.BranchCode);
        }
        catch (ArgumentException)
        {
            return new CounterSaleResult(CounterSaleStatus.Forbidden);
        }

        var branch = await _branchRepository.GetByCodeAsync(branchCode, cancellationToken);
        if (branch is null)
        {
            return new CounterSaleResult(CounterSaleStatus.BranchNotFound);
        }

        if (!branch.IsOpenAt(TimeOnly.FromDateTime(_timeProvider.GetLocalNow().DateTime)))
        {
            return new CounterSaleResult(CounterSaleStatus.BranchClosed);
        }

        var counter = await _counterRepository.GetByIdAsync(request.CounterId, cancellationToken);
        if (counter is null || counter.BranchCode != branchCode)
        {
            return new CounterSaleResult(CounterSaleStatus.CounterNotFound);
        }

        if (!counter.IsActive)
        {
            return new CounterSaleResult(CounterSaleStatus.CounterInactive);
        }

        var products = new List<(Product Product, int Quantity)>();
        var quantities = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        foreach (var line in request.Products)
        {
            Sku sku;
            try
            {
                sku = new Sku(line.Sku);
            }
            catch (ArgumentException)
            {
                return new CounterSaleResult(CounterSaleStatus.InvalidProduct);
            }

            var quantity = checked(quantities.GetValueOrDefault(sku.Value) + line.Quantity);
            quantities[sku.Value] = quantity;
        }

        foreach (var (sku, quantity) in quantities)
        {
            var product = await _productRepository.GetBySkuAsync(sku, cancellationToken);
            if (product is null)
            {
                return new CounterSaleResult(CounterSaleStatus.InvalidProduct);
            }

            products.Add((product, quantity));
        }

        Money? shippingPrice = null;
        if (request.Shipping is not null)
        {
            if (!Enum.IsDefined(request.Shipping.ServiceLevel))
            {
                return new CounterSaleResult(CounterSaleStatus.ShippingUnavailable);
            }

            try
            {
                shippingPrice = _shippingService.CalculatePrice(
                    request.Shipping.DestinationZone,
                    request.Shipping.ServiceLevel,
                    new Weight(request.Shipping.WeightKg));
                _ = new Shipment(
                    "validation-only",
                    request.Shipping.ItemType,
                    new Weight(request.Shipping.WeightKg),
                    request.Shipping.LengthCm,
                    request.Shipping.WidthCm,
                    request.Shipping.HeightCm,
                    request.Shipping.DestinationZone,
                    request.Shipping.ServiceLevel);
            }
            catch (ArgumentException)
            {
                return new CounterSaleResult(CounterSaleStatus.ShippingUnavailable);
            }
            catch (InvalidOperationException)
            {
                return new CounterSaleResult(CounterSaleStatus.ShippingUnavailable);
            }
        }

        var transactionId = $"TXN-{Guid.NewGuid():N}";
        var shipmentId = request.Shipping is null ? null : $"SHP-{Guid.NewGuid():N}";
        var transaction = new CounterTransaction(
            transactionId,
            branchCode,
            counter.Id,
            staff.Id,
            _taxRates);

        foreach (var (product, quantity) in products)
        {
            transaction.AddLineItem(new TransactionLineItem(
                product.Name,
                "Retail",
                quantity,
                product.UnitPrice,
                product.TaxCategory));
        }

        if (shippingPrice is not null)
        {
            var shipping = request.Shipping!;
            transaction.AddLineItem(new TransactionLineItem(
                $"Shipping ({shipping.ItemType}, {shipping.DestinationZone}, {shipping.ServiceLevel}, {shipmentId})",
                "Service",
                1,
                shippingPrice,
                TaxCategory.Standard,
                shipmentId));
        }

        try
        {
            transaction.CompletePayment(request.PaymentMethod);
        }
        catch (ArgumentException)
        {
            return new CounterSaleResult(CounterSaleStatus.InvalidRequest);
        }
        catch (InvalidOperationException)
        {
            return new CounterSaleResult(CounterSaleStatus.InvalidRequest);
        }

        InventoryReservation? inventoryReservation = null;
        if (quantities.Count > 0)
        {
            var reservationResult = await _inventoryRepository.TryReserveAsync(
                branchCode.Value,
                quantities,
                cancellationToken);
            if (reservationResult.Status != InventoryReservationStatus.Reserved)
            {
                return new CounterSaleResult(CounterSaleStatus.InsufficientStock);
            }

            inventoryReservation = reservationResult.Reservation
                ?? throw new InvalidOperationException("A successful stock reservation must include its rollback token.");
        }

        var shipmentPersisted = false;
        try
        {
            if (request.Shipping is not null)
            {
                var shipping = request.Shipping;
                await _shippingService.CreateShipmentAsync(
                    shipmentId!,
                    shipping.ItemType,
                    shipping.WeightKg,
                    shipping.LengthCm,
                    shipping.WidthCm,
                    shipping.HeightCm,
                    shipping.DestinationZone,
                    shipping.ServiceLevel,
                    cancellationToken,
                    branchCode,
                    transactionId);
                shipmentPersisted = true;
            }

            await _transactionRepository.AddAsync(transaction, cancellationToken);
            if (inventoryReservation is not null)
            {
                await _inventoryRepository.CommitReservationAsync(
                    inventoryReservation,
                    CancellationToken.None);
            }

            return new CounterSaleResult(CounterSaleStatus.Completed, transaction, shipmentId);
        }
        catch (Exception persistenceException)
        {
            try
            {
                if (shipmentPersisted)
                {
                    await _shipmentRepository.DeleteAsync(shipmentId!, CancellationToken.None);
                }

                if (inventoryReservation is not null)
                {
                    await _inventoryRepository.RestoreReservationAsync(
                        inventoryReservation,
                        CancellationToken.None);
                }
            }
            catch (Exception rollbackException)
            {
                throw new InvalidOperationException(
                    "A counter sale failed and its compensating rollback could not be confirmed.",
                    new AggregateException(persistenceException, rollbackException));
            }

            if (persistenceException is OperationCanceledException)
            {
                throw;
            }

            return new CounterSaleResult(CounterSaleStatus.PersistenceFailure);
        }
    }

    public async Task<CounterSaleOptions?> GetOptionsAsync(
        AuthenticatedStaff authenticatedStaff,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(authenticatedStaff);
        var staff = await _staffRepository.GetByIdAsync(authenticatedStaff.Id, cancellationToken);
        if (staff is null || !staff.IsActive ||
            staff.Role != authenticatedStaff.Role ||
            staff.HomeBranchCode.Value != authenticatedStaff.BranchCode ||
            staff.Role is not (StaffRole.CounterClerk or StaffRole.BranchManager or StaffRole.Administrator))
        {
            return null;
        }

        var branchCode = new BranchCode(staff.HomeBranchCode.Value);
        var branch = await _branchRepository.GetByCodeAsync(branchCode, cancellationToken);
        if (branch is null || !branch.IsOpenAt(TimeOnly.FromDateTime(_timeProvider.GetLocalNow().DateTime)))
        {
            return null;
        }

        var productsTask = _productRepository.GetAllAsync(cancellationToken);
        var countersTask = _counterRepository.GetByBranchAsync(branchCode.Value, cancellationToken);
        await Task.WhenAll(productsTask, countersTask);
        var products = await productsTask;
        var counters = await countersTask;

        return new CounterSaleOptions(
            products,
            counters.Where(counter => counter.IsActive).ToArray());
    }

    public async Task<CounterSaleResult> GetReceiptAsync(
        string transactionId,
        AuthenticatedStaff authenticatedStaff,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(authenticatedStaff);
        if (authenticatedStaff.Role is not (StaffRole.CounterClerk or StaffRole.BranchManager or StaffRole.Administrator))
        {
            return new CounterSaleResult(CounterSaleStatus.Forbidden);
        }

        var staff = await _staffRepository.GetByIdAsync(authenticatedStaff.Id, cancellationToken);
        if (staff is null || !staff.IsActive ||
            staff.Role != authenticatedStaff.Role ||
            staff.HomeBranchCode.Value != authenticatedStaff.BranchCode)
        {
            return new CounterSaleResult(CounterSaleStatus.Forbidden);
        }

        var transaction = await _transactionRepository.GetByIdAsync(transactionId, cancellationToken);
        if (transaction is null)
        {
            return new CounterSaleResult(CounterSaleStatus.TransactionNotFound);
        }

        if (authenticatedStaff.Role == StaffRole.Administrator)
        {
            return new CounterSaleResult(CounterSaleStatus.Completed, transaction);
        }

        if (!string.Equals(
                transaction.BranchCode.Value,
                authenticatedStaff.BranchCode,
                StringComparison.OrdinalIgnoreCase) ||
            (authenticatedStaff.Role == StaffRole.CounterClerk &&
             transaction.ClerkId != authenticatedStaff.Id))
        {
            return new CounterSaleResult(CounterSaleStatus.Forbidden);
        }

        return new CounterSaleResult(CounterSaleStatus.Completed, transaction);
    }

    private static bool IsValidRequest(CounterSaleRequest request) =>
        !string.IsNullOrWhiteSpace(request.CounterId) &&
        request.CounterId.Length <= 128 &&
        request.Products is { Count: <= 50 } &&
        (request.Products.Count > 0 || request.Shipping is not null) &&
        request.Products.All(line => line is not null &&
            !string.IsNullOrWhiteSpace(line.Sku) &&
            line.Sku.Length <= 20 &&
            line.Quantity is > 0 and <= 1000) &&
        Enum.IsDefined(request.PaymentMethod) &&
        (request.Shipping is null ||
         (!string.IsNullOrWhiteSpace(request.Shipping.ItemType) &&
          request.Shipping.ItemType.Length <= 16 &&
          !string.IsNullOrWhiteSpace(request.Shipping.DestinationZone) &&
          request.Shipping.DestinationZone.Length <= 32));
}
