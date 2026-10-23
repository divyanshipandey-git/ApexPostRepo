
using ApexPost.Domain.Entities;
using ApexPost.Domain.ValueObjects;
using Xunit;

namespace ApexPost.Domain.Tests.Entities;

public class InventoryStockTests
{
    [Fact]
    public void Constructor_ShouldCreateStockWithValidQuantity()
    {
        var stock = CreateStock(10);

        Assert.Equal(10, stock.Quantity);
        Assert.Equal("IND01", stock.BranchCode.Value);
        Assert.Equal("PACK001", stock.Sku.Value);
    }

    [Fact]
    public void Constructor_ShouldRejectNegativeQuantity()
    {
        Assert.Throws<ArgumentOutOfRangeException>(
            () => CreateStock(-1));
    }

    [Fact]
    public void AddStock_ShouldIncreaseQuantity()
    {
        var stock = CreateStock(10);

        stock.AddStock(5);

        Assert.Equal(15, stock.Quantity);
    }

    [Fact]
    public void AddStock_ShouldRejectZeroQuantity()
    {
        var stock = CreateStock(10);

        Assert.Throws<ArgumentOutOfRangeException>(
            () => stock.AddStock(0));
    }

    [Fact]
    public void RemoveStock_ShouldDecreaseQuantity()
    {
        var stock = CreateStock(10);

        stock.RemoveStock(3);

        Assert.Equal(7, stock.Quantity);
    }

    [Fact]
    public void RemoveStock_ShouldRejectInsufficientStock()
    {
        var stock = CreateStock(5);

        Assert.Throws<InvalidOperationException>(
            () => stock.RemoveStock(6));

        Assert.Equal(5, stock.Quantity);
    }

    [Fact]
    public void RemoveStock_ShouldAllowStockToReachZero()
    {
        var stock = CreateStock(5);

        stock.RemoveStock(5);

        Assert.Equal(0, stock.Quantity);
    }

    private static InventoryStock CreateStock(int quantity) =>
        new(
            new BranchCode("IND01"),
            new Sku("PACK001"),
            quantity);
}
