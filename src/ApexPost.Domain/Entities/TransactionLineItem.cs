
using ApexPost.Domain.ValueObjects;

namespace ApexPost.Domain.Entities;

public sealed class TransactionLineItem
{
    public string Description { get; }
    public string ItemType { get; }
    public int Quantity { get; }
    public Money UnitPrice { get; }
    public TaxCategory TaxCategory { get; }

    public TransactionLineItem(
        string description,
        string itemType,
        int quantity,
        Money unitPrice,
        TaxCategory taxCategory = TaxCategory.Exempt)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(description);
        ArgumentException.ThrowIfNullOrWhiteSpace(itemType);
        ArgumentNullException.ThrowIfNull(unitPrice);

        if (itemType is not ("Retail" or "Service"))
        {
            throw new ArgumentException(
                "Item type must be Retail or Service.",
                nameof(itemType));
        }

        if (quantity <= 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(quantity),
                "Quantity must be greater than zero.");
        }

        if (unitPrice.Amount < 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(unitPrice),
                "Unit price cannot be negative.");
        }

        if (!Enum.IsDefined(taxCategory))
        {
            throw new ArgumentOutOfRangeException(nameof(taxCategory));
        }

        Description = description.Trim();
        ItemType = itemType;
        Quantity = quantity;
        UnitPrice = unitPrice;
        TaxCategory = taxCategory;
    }

    public Money LineTotal => new(UnitPrice.Amount * Quantity);
}
