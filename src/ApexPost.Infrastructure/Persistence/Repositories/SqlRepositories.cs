using System.Collections.ObjectModel;
using ApexPost.Application.Abstractions.Repositories;
using ApexPost.Application.Abstractions.Reports;
using ApexPost.Application.Reports;
using ApexPost.Domain.Entities;
using ApexPost.Domain.ValueObjects;
using ApexPost.Infrastructure.Persistence.SqlEntities;
using Microsoft.EntityFrameworkCore;

namespace ApexPost.Infrastructure.Persistence.Repositories;

public sealed class SqlBranchRepository(ApexPostDbContext db) : IBranchRepository
{
    public Task<Branch?> GetByCodeAsync(BranchCode code, CancellationToken cancellationToken = default) =>
        db.Branches.AsNoTracking().SingleOrDefaultAsync(item => item.Code == code, cancellationToken);
    public async Task<IReadOnlyList<Branch>> GetAllAsync(CancellationToken cancellationToken = default) =>
        await db.Branches.AsNoTracking().ToArrayAsync(cancellationToken);
    public Task AddAsync(Branch branch, CancellationToken cancellationToken = default) =>
        db.Branches.AddAsync(branch, cancellationToken).AsTask();
    public Task UpdateAsync(Branch branch, CancellationToken cancellationToken = default)
    {
        db.Branches.Attach(branch);
        db.Entry(branch).State = EntityState.Modified;
        return Task.CompletedTask;
    }
}

public sealed class SqlCounterRepository(ApexPostDbContext db) : ICounterRepository
{
    public Task<Counter?> GetByIdAsync(string id, CancellationToken cancellationToken = default) =>
        db.Counters.AsNoTracking().SingleOrDefaultAsync(item => item.Id == id, cancellationToken);
    public async Task<IReadOnlyList<Counter>> GetByBranchAsync(string branchCode, CancellationToken cancellationToken = default) =>
        await db.Counters.AsNoTracking().Where(item => item.BranchCode == new BranchCode(branchCode)).ToArrayAsync(cancellationToken);
    public Task AddAsync(Counter item, CancellationToken cancellationToken = default) => db.Counters.AddAsync(item, cancellationToken).AsTask();
    public Task UpdateAsync(Counter item, CancellationToken cancellationToken = default)
    {
        db.Counters.Attach(item); db.Entry(item).State = EntityState.Modified; return Task.CompletedTask;
    }
}

public sealed class SqlStaffRepository(ApexPostDbContext db) : IStaffRepository
{
    public Task<Staff?> GetByIdAsync(string id, CancellationToken cancellationToken = default) =>
        db.Staff.AsNoTracking().SingleOrDefaultAsync(item => item.Id == id, cancellationToken);
    public async Task<IReadOnlyList<Staff>> GetAllAsync(CancellationToken cancellationToken = default) =>
        await db.Staff.AsNoTracking().ToArrayAsync(cancellationToken);
    public async Task<IReadOnlyList<Staff>> GetByBranchAsync(string branchCode, CancellationToken cancellationToken = default) =>
        await db.Staff.AsNoTracking().Where(item => item.HomeBranchCode == new BranchCode(branchCode)).ToArrayAsync(cancellationToken);
    public Task AddAsync(Staff item, CancellationToken cancellationToken = default) => db.Staff.AddAsync(item, cancellationToken).AsTask();
    public Task UpdateAsync(Staff item, CancellationToken cancellationToken = default)
    {
        db.Staff.Attach(item); db.Entry(item).State = EntityState.Modified; return Task.CompletedTask;
    }
}

public sealed class SqlStaffCredentialRepository(ApexPostDbContext db) : IStaffCredentialRepository
{
    public async Task AddPasswordHashAsync(string staffId, string passwordHash, CancellationToken cancellationToken = default) =>
        await db.StaffCredentials.AddAsync(new StaffCredential(staffId, passwordHash), cancellationToken);
    public async Task<string?> GetPasswordHashAsync(string staffId, CancellationToken cancellationToken = default) =>
        await db.StaffCredentials.AsNoTracking().Where(item => item.StaffId == staffId).Select(item => item.PasswordHash).SingleOrDefaultAsync(cancellationToken);
}

public sealed class SqlProductRepository(ApexPostDbContext db) : IProductRepository
{
    public Task<Product?> GetBySkuAsync(string sku, CancellationToken cancellationToken = default) =>
        db.Products.AsNoTracking().SingleOrDefaultAsync(item => item.Sku == new Sku(sku), cancellationToken);
    public async Task<IReadOnlyList<Product>> GetAllAsync(CancellationToken cancellationToken = default) =>
        await db.Products.AsNoTracking().ToArrayAsync(cancellationToken);
}

public sealed class SqlInventoryRepository(ApexPostDbContext db) : IStockRepository
{
    private readonly Dictionary<Guid, InventoryReservation> _reservations = [];

    public async Task<InventoryReservationResult> TryReserveAsync(string branchCode, IReadOnlyDictionary<string, int> quantities, CancellationToken cancellationToken = default)
    {
        var branch = new BranchCode(branchCode);
        var normalized = Normalize(quantities);
        var skus = normalized.Keys.Select(sku => new Sku(sku)).ToArray();
        var stocks = await db.BranchStock.Where(stock => stock.BranchCode == branch && skus.Contains(stock.Sku)).ToArrayAsync(cancellationToken);
        if (stocks.Length != normalized.Count || stocks.Any(stock => stock.Quantity < normalized[stock.Sku.Value]))
            return new(InventoryReservationStatus.StockUnavailable);
        foreach (var stock in stocks) stock.RemoveStock(normalized[stock.Sku.Value]);
        var reservation = new InventoryReservation(Guid.NewGuid(), branch.Value, new ReadOnlyDictionary<string, int>(normalized));
        _reservations.Add(reservation.Id, reservation);
        return new(InventoryReservationStatus.Reserved, reservation);
    }

    public Task RestoreReservationAsync(InventoryReservation reservation, CancellationToken cancellationToken = default)
    {
        if (!_reservations.TryGetValue(reservation.Id, out var pending)) throw new InvalidOperationException("The stock reservation is unknown or already completed.");
        foreach (var (sku, quantity) in pending.SkuQuantities)
        {
            var stock = db.BranchStock.Local.SingleOrDefault(item => item.BranchCode.Value == pending.BranchCode && item.Sku.Value == sku)
                ?? db.BranchStock.SingleOrDefault(item => item.BranchCode == new BranchCode(pending.BranchCode) && item.Sku == new Sku(sku));
            if (stock is null) throw new InvalidOperationException("Reserved stock is no longer available for restoration.");
            stock.AddStock(quantity);
        }
        return Task.CompletedTask;
    }

    public async Task RestoreReversedRetailStockAsync(string transactionId, string branchCode, IReadOnlyDictionary<string, int> quantities, CancellationToken cancellationToken = default)
        => await RestoreReversedRetailStockAsync(transactionId, branchCode, quantities, "Transaction reversal", "Unknown", cancellationToken);

    public async Task RestoreReversedRetailStockAsync(string transactionId, string branchCode, IReadOnlyDictionary<string, int> quantities, string reason, string changedBy, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(reason);
        ArgumentException.ThrowIfNullOrWhiteSpace(changedBy);
        if (await db.StockMovements.AnyAsync(m => m.TransactionId == transactionId && m.MovementType == "ReversalRestore", cancellationToken)) return;
        if (db.StockMovements.Local.Any(m => m.TransactionId == transactionId && m.MovementType == "ReversalRestore")) return;
        var branch = new BranchCode(branchCode);
        var normalized = Normalize(quantities);
        var skus = normalized.Keys.Select(sku => new Sku(sku)).ToArray();
        var stocks = (await db.BranchStock.Where(stock => stock.BranchCode == branch && skus.Contains(stock.Sku)).ToArrayAsync(cancellationToken))
            .ToDictionary(stock => stock.Sku.Value, StringComparer.OrdinalIgnoreCase);
        foreach (var (skuText, quantity) in normalized)
        {
            if (!stocks.TryGetValue(skuText, out var stock))
            {
                stock = new InventoryStock(branch, new Sku(skuText), quantity);
                db.BranchStock.Add(stock);
            }
            else stock.AddStock(quantity);
            var before = stock.Quantity - quantity;
            db.StockMovements.Add(new StockMovementRecord { BranchCode = branch, Sku = stock.Sku, QuantityDelta = quantity, QuantityBefore = before, QuantityAfter = stock.Quantity, MovementType = "ReversalRestore", TransactionId = transactionId, Reason = reason.Trim(), ChangedBy = changedBy.Trim() });
        }
    }

    public Task CommitReservationAsync(InventoryReservation reservation, CancellationToken cancellationToken = default)
    {
        if (!_reservations.ContainsKey(reservation.Id)) throw new InvalidOperationException("The stock reservation is unknown or already completed.");
        return Task.CompletedTask;
    }
    public Task<InventoryStock?> GetByBranchAndSkuAsync(string branchCode, string sku, CancellationToken cancellationToken = default) =>
        db.BranchStock.AsNoTracking().SingleOrDefaultAsync(stock => stock.BranchCode == new BranchCode(branchCode) && stock.Sku == new Sku(sku), cancellationToken);
    public async Task<IReadOnlyList<InventoryStock>> GetByBranchAsync(string branchCode, CancellationToken cancellationToken = default) =>
        await db.BranchStock.AsNoTracking().Where(stock => stock.BranchCode == new BranchCode(branchCode)).ToArrayAsync(cancellationToken);
    public Task AddAsync(InventoryStock stock, CancellationToken cancellationToken = default) => db.BranchStock.AddAsync(stock, cancellationToken).AsTask();
    public async Task UpdateAsync(InventoryStock stock, CancellationToken cancellationToken = default)
    {
        var tracked = await db.BranchStock.SingleOrDefaultAsync(
            current => current.BranchCode == stock.BranchCode && current.Sku == stock.Sku,
            cancellationToken) ?? throw new KeyNotFoundException("The branch stock row was not found.");
        var delta = stock.Quantity - tracked.Quantity;
        if (delta > 0) tracked.AddStock(delta);
        else if (delta < 0) tracked.RemoveStock(checked(-delta));
    }
    public async Task<InventoryStock?> AdjustQuantityAsync(string branchCode, string sku, int adjustment, CancellationToken cancellationToken = default)
        => await AdjustQuantityWithAuditAsync(branchCode, sku, adjustment, "Stock adjustment", "Unknown", cancellationToken);

    public async Task<InventoryStock?> AdjustQuantityWithAuditAsync(string branchCode, string sku, int adjustment, string reason, string changedBy, CancellationToken cancellationToken = default)
    {
        var branch = new BranchCode(branchCode); var productSku = new Sku(sku);
        ArgumentException.ThrowIfNullOrWhiteSpace(reason); ArgumentException.ThrowIfNullOrWhiteSpace(changedBy);
        if (adjustment == 0 || adjustment == int.MinValue) throw new ArgumentOutOfRangeException(nameof(adjustment));
        var existing = await db.BranchStock.AsNoTracking().AnyAsync(item => item.BranchCode == branch && item.Sku == productSku, cancellationToken);
        if (!existing && adjustment < 0) return null;
        await db.Database.ExecuteSqlInterpolatedAsync($"EXEC dbo.usp_AdjustStock @BranchCode={branch.Value}, @Sku={productSku.Value}, @Delta={adjustment}, @Reason={reason.Trim()}, @ChangedBy={changedBy.Trim()}", cancellationToken);
        return await db.BranchStock.AsNoTracking().SingleOrDefaultAsync(item => item.BranchCode == branch && item.Sku == productSku, cancellationToken);
    }
    private static Dictionary<string, int> Normalize(IReadOnlyDictionary<string, int> quantities)
    {
        ArgumentNullException.ThrowIfNull(quantities);
        var result = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        foreach (var pair in quantities)
        {
            var sku = new Sku(pair.Key).Value;
            if (pair.Value <= 0) throw new ArgumentOutOfRangeException(nameof(quantities));
            result[sku] = checked(result.GetValueOrDefault(sku) + pair.Value);
        }
        if (result.Count == 0) throw new ArgumentException("At least one SKU is required.", nameof(quantities));
        return result;
    }
}

public sealed class SqlCounterTransactionRepository(ApexPostDbContext db) : ICounterTransactionRepository
{
    private IQueryable<CounterTransactionRecord> Full => db.CounterTransactions.Include(tx => tx.Lines).Include(tx => tx.TaxRates).Include(tx => tx.Payment);
    public async Task<CounterTransaction?> GetByIdAsync(string id, CancellationToken cancellationToken = default)
    {
        var record = await Full.AsNoTracking().SingleOrDefaultAsync(tx => tx.Id == id, cancellationToken);
        return record is null ? null : SqlEntityMapper.ToDomain(record);
    }
    public async Task<IReadOnlyList<CounterTransaction>> GetByBranchAsync(string branchCode, CancellationToken cancellationToken = default)
    {
        var branch = new BranchCode(branchCode);
        var records = await Full.AsNoTracking().Where(tx => tx.BranchCode == branch).ToArrayAsync(cancellationToken);
        return records.Select(SqlEntityMapper.ToDomain).ToArray();
    }
    public Task AddAsync(CounterTransaction transaction, CancellationToken cancellationToken = default) => db.CounterTransactions.AddAsync(SqlEntityMapper.ToRecord(transaction), cancellationToken).AsTask();
    public async Task UpdateAsync(CounterTransaction transaction, CancellationToken cancellationToken = default)
    {
        var existing = await Full.SingleOrDefaultAsync(tx => tx.Id == transaction.Id, cancellationToken)
            ?? throw new KeyNotFoundException($"Transaction '{transaction.Id}' was not found.");
        var updated = SqlEntityMapper.ToRecord(transaction);
        existing.Status = updated.Status; existing.Reason = updated.Reason;
    }
}

public sealed class SqlShipmentRepository(ApexPostDbContext db) : IShipmentRepository
{
    public Task<Shipment?> GetByIdAsync(string id, CancellationToken cancellationToken = default) => db.Shipments.AsNoTracking().SingleOrDefaultAsync(item => item.Id == id, cancellationToken);
    public async Task<IReadOnlyList<Shipment>> GetByBranchAsync(string branchCode, CancellationToken cancellationToken = default) => await db.Shipments.AsNoTracking().Where(item => item.BranchCode == new BranchCode(branchCode)).ToArrayAsync(cancellationToken);
    public Task AddAsync(Shipment item, CancellationToken cancellationToken = default) => db.Shipments.AddAsync(item, cancellationToken).AsTask();
    public Task UpdateAsync(Shipment item, CancellationToken cancellationToken = default) { db.Shipments.Attach(item); db.Entry(item).State = EntityState.Modified; return Task.CompletedTask; }
    public async Task DeleteAsync(string id, CancellationToken cancellationToken = default)
    {
        var local = db.Shipments.Local.SingleOrDefault(item => item.Id == id);
        var item = local ?? await db.Shipments.SingleOrDefaultAsync(x => x.Id == id, cancellationToken);
        if (item is not null) db.Shipments.Remove(item);
    }
}

public sealed class SqlMoneyOrderRepository(ApexPostDbContext db) : IMoneyOrderRepository
{
    public async Task<MoneyOrder?> GetByIdAsync(string id, CancellationToken cancellationToken = default) { var row = await db.MoneyOrders.AsNoTracking().Include(x => x.IdentityVerification).SingleOrDefaultAsync(x => x.Id == id, cancellationToken); return row is null ? null : SqlEntityMapper.ToDomain(row); }
    public async Task<IReadOnlyList<MoneyOrder>> GetByBranchAsync(string branchCode, CancellationToken cancellationToken = default) { var branch = new BranchCode(branchCode); var rows = await db.MoneyOrders.AsNoTracking().Include(x => x.IdentityVerification).Where(x => x.BranchCode == branch).ToArrayAsync(cancellationToken); return rows.Select(SqlEntityMapper.ToDomain).ToArray(); }
    public Task AddAsync(MoneyOrder order, CancellationToken cancellationToken = default) => db.MoneyOrders.AddAsync(SqlEntityMapper.ToRecord(order), cancellationToken).AsTask();
    public async Task UpdateAsync(MoneyOrder order, CancellationToken cancellationToken = default) { var row = await db.MoneyOrders.Include(x => x.IdentityVerification).SingleOrDefaultAsync(x => x.Id == order.Id, cancellationToken) ?? throw new KeyNotFoundException(); var updated = SqlEntityMapper.ToRecord(order); db.Entry(row).CurrentValues.SetValues(updated); if (row.IdentityVerification is not null) db.IdentityVerifications.Remove(row.IdentityVerification); row.IdentityVerification = updated.IdentityVerification; }
    public async Task DeleteAsync(string id, CancellationToken cancellationToken = default) { var local = db.MoneyOrders.Local.SingleOrDefault(item => item.Id == id); var row = local ?? await db.MoneyOrders.Include(x => x.IdentityVerification).SingleOrDefaultAsync(x => x.Id == id, cancellationToken); if (row is not null) db.MoneyOrders.Remove(row); }
}

public sealed class SqlReportDataSource(ApexPostDbContext db) : IReportDataSource
{
    public async Task<IReadOnlyList<ReportDailyBranchSales>> GetDailyBranchSalesAsync(
        DateOnly date,
        string? branchCode = null,
        CancellationToken cancellationToken = default)
    {
        var rows = await db.Database.SqlQuery<DailyBranchSalesProcedureRow>(
                $"EXEC dbo.usp_DailyBranchSales @ReportDate={date}, @BranchCode={branchCode}")
            .ToArrayAsync(cancellationToken);
        return rows.Select(row => new ReportDailyBranchSales(
            row.BranchCode, DateOnly.FromDateTime(row.SaleDate), checked((int)row.TransactionCount),
            row.RetailSales, row.ServiceSales)).ToArray();
    }

    public async Task<IReadOnlyList<LowStockReportItem>> GetLowStockAsync(
        string? branchCode = null,
        CancellationToken cancellationToken = default)
    {
        var rows = await db.Database.SqlQuery<LowStockProcedureRow>(
                $"EXEC dbo.usp_LowStockReport @ReorderLevel={5}, @BranchCode={branchCode}")
            .ToArrayAsync(cancellationToken);
        return rows.Select(row => new LowStockReportItem(
            row.BranchCode, row.Sku, row.ProductName, row.Quantity, row.ReorderLevel)).ToArray();
    }

    public async Task<IReadOnlyList<ReportTransaction>> GetTransactionsAsync(CancellationToken cancellationToken = default)
    {
        var query = from transaction in db.CounterTransactions.AsNoTracking()
                    join line in db.TransactionLines.AsNoTracking() on transaction.Id equals line.TransactionId
                    join taxRate in db.TransactionTaxRates.AsNoTracking()
                        on new { line.TransactionId, line.TaxCategory }
                        equals new { taxRate.TransactionId, TaxCategory = taxRate.Category } into taxRates
                    from taxRate in taxRates.DefaultIfEmpty()
                    where transaction.Status == TransactionStatus.Paid && transaction.OccurredAtUtc != null
                        && (line.ItemType == "Retail" || line.ItemType == "Service")
                    group new { line, taxRate } by new
                    {
                        transaction.Id,
                        Branch = transaction.BranchCode,
                        At = transaction.OccurredAtUtc!.Value,
                        Category = line.ItemType
                    } into grouped
                    select new
                    {
                        grouped.Key.Id,
                        grouped.Key.Branch,
                        grouped.Key.At,
                        grouped.Key.Category,
                        Gross = grouped.Sum(item => item.line.LineTotal + item.line.LineTotal * (item.taxRate == null ? 0m : item.taxRate.Rate))
                    };
        var rows = await query.ToArrayAsync(cancellationToken);
        return rows.Select(row => new ReportTransaction(row.Id, row.Branch.Value, row.At,
            row.Category == "Retail" ? ReportSaleCategory.Retail : ReportSaleCategory.Service, row.Gross, true)).ToArray();
    }
    public async Task<IReadOnlyList<ReportInventoryItem>> GetInventoryAsync(CancellationToken cancellationToken = default) =>
        await (from stock in db.BranchStock.AsNoTracking() join product in db.Products.AsNoTracking() on stock.Sku equals product.Sku select new ReportInventoryItem(stock.BranchCode.Value, stock.Sku.Value, product.Name, stock.Quantity, 5)).ToArrayAsync(cancellationToken);
    public async Task<IReadOnlyList<ReportShipment>> GetShipmentsAsync(CancellationToken cancellationToken = default) =>
        await db.Shipments.AsNoTracking().Where(s => s.BranchCode != null && s.CreatedAtUtc != null).Select(s => new ReportShipment(s.BranchCode!.Value, s.DestinationZone, s.ServiceLevel.ToString(), s.CreatedAtUtc!.Value)).ToArrayAsync(cancellationToken);
    public async Task<IReadOnlyList<ReportMoneyOrder>> GetMoneyOrdersAsync(CancellationToken cancellationToken = default) =>
        await db.MoneyOrders.AsNoTracking().Where(x => x.BranchCode != null && x.CreatedAtUtc != null).Select(x => new ReportMoneyOrder(x.BranchCode!.Value, x.Id, x.CreatedAtUtc!.Value)).ToArrayAsync(cancellationToken);
    public async Task<IReadOnlyList<ReportIdentityVerification>> GetIdentityVerificationsAsync(CancellationToken cancellationToken = default) =>
        await (from order in db.MoneyOrders.AsNoTracking() join identity in db.IdentityVerifications.AsNoTracking() on order.Id equals identity.MoneyOrderId where order.BranchCode != null select new ReportIdentityVerification(order.BranchCode!.Value, identity.Id, identity.VerifiedAtUtc)).ToArrayAsync(cancellationToken);
}

public sealed class DailyBranchSalesProcedureRow
{
    public string BranchCode { get; set; } = string.Empty;
    public DateTime SaleDate { get; set; }
    public long TransactionCount { get; set; }
    public decimal RetailSales { get; set; }
    public decimal ServiceSales { get; set; }
    public decimal TotalSales { get; set; }
}

public sealed class LowStockProcedureRow
{
    public string BranchCode { get; set; } = string.Empty;
    public string Sku { get; set; } = string.Empty;
    public string ProductName { get; set; } = string.Empty;
    public int Quantity { get; set; }
    public int ReorderLevel { get; set; }
}
