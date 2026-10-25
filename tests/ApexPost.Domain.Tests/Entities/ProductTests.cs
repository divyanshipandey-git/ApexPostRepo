
using ApexPost.Domain.Entities;
using ApexPost.Domain.ValueObjects;
using Xunit;

namespace ApexPost.Domain.Tests.Entities;

public class ProductTests
{
    [Fact]
    public void Constructor_ShouldCreateValidProduct()
    {
        var product = CreateProduct();

        Assert.Equal("PACK001", product.Sku.Value);
        Assert.Equal("Small Box", product.Name);
        Assert.Equal(ProductCategory.Packaging, product.Category);
        Assert.Equal(50m, product.UnitPrice.Amount);
        Assert.Equal(TaxCategory.Standard, product.TaxCategory);
    }

    [Fact]
    public void Constructor_ShouldTrimProductName()
    {
        var product = new Product(
            new Sku("PACK001"),
            " Small Box ",
            ProductCategory.Packaging,
            new Money(50m),
            TaxCategory.Standard);

        Assert.Equal("Small Box", product.Name);
    }

    [Fact]
    public void Constructor_ShouldRejectEmptyName()
    {
        Assert.Throws<ArgumentException>(() =>
            new Product(
                new Sku("PACK001"),
                " ",
                ProductCategory.Packaging,
                new Money(50m),
                TaxCategory.Standard));
    }

    [Fact]
    public void Constructor_ShouldRejectNegativePrice()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new Product(
                new Sku("PACK001"),
                "Small Box",
                ProductCategory.Packaging,
                new Money(-1m),
                TaxCategory.Standard));
    }

    [Fact]
    public void Constructor_ShouldRejectUndefinedCategory()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new Product(
                new Sku("PACK001"),
                "Small Box",
                (ProductCategory)999,
                new Money(50m),
                TaxCategory.Standard));
    }

    private static Product CreateProduct() =>
        new(
            new Sku("PACK001"),
            "Small Box",
            ProductCategory.Packaging,
            new Money(50m),
            TaxCategory.Standard);
}
