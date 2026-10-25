
using ApexPost.Domain.ValueObjects;

namespace ApexPost.Domain.Entities;

public sealed class Product
{
    public Sku Sku { get; }
    public string Name { get; }
    public ProductCategory Category { get; }
    public Money UnitPrice { get; }
    public TaxCategory TaxCategory { get; }

    public Product(
        Sku sku,
        string name,
        ProductCategory category,
        Money unitPrice,
        TaxCategory taxCategory)
    {
        ArgumentNullException.ThrowIfNull(sku);
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentNullException.ThrowIfNull(unitPrice);

        if (!Enum.IsDefined(category))
            throw new ArgumentOutOfRangeException(nameof(category));

        if (!Enum.IsDefined(taxCategory))
            throw new ArgumentOutOfRangeException(nameof(taxCategory));

        if (unitPrice.Amount < 0)
            throw new ArgumentOutOfRangeException(
                nameof(unitPrice),
                "Product price cannot be negative.");

        Sku = sku;
        Name = name.Trim();
        Category = category;
        UnitPrice = unitPrice;
        TaxCategory = taxCategory;
    }
}
