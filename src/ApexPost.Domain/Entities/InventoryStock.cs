
using ApexPost.Domain.ValueObjects;

namespace ApexPost.Domain.Entities;

public sealed class InventoryStock
{
    public BranchCode BranchCode { get; }
    public Sku Sku { get; }
    public int Quantity { get; private set; }

    public InventoryStock(
        BranchCode branchCode,
        Sku sku,
        int quantity)
    {
        ArgumentNullException.ThrowIfNull(branchCode);
        ArgumentNullException.ThrowIfNull(sku);

        if (quantity < 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(quantity),
                "Stock quantity cannot be negative.");
        }

        BranchCode = branchCode;
        Sku = sku;
        Quantity = quantity;
    }

    public void AddStock(int quantity)
    {
        if (quantity <= 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(quantity),
                "Quantity to add must be positive.");
        }

        Quantity = checked(Quantity + quantity);
    }

    public void RemoveStock(int quantity)
    {
        if (quantity <= 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(quantity),
                "Quantity to remove must be positive.");
        }

        if (quantity > Quantity)
        {
            throw new InvalidOperationException(
                "Insufficient stock available.");
        }

        Quantity -= quantity;
    }
}
