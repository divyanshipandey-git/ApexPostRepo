using ApexPost.Domain.Entities;
using ApexPost.Domain.ValueObjects;
using ApexPost.Specs.Support;
using Reqnroll;
using Xunit;

namespace ApexPost.Specs.StepDefinitions;

/// <summary>Steps of Inventory.feature. SERVICE level: the real InventoryService and stock rules, in-memory store, no HTTP.</summary>
[Binding]
public sealed class InventorySteps(InventoryScenarioContext context)
{
    // ------------------------------------------------------------------ given

    [Given(@"branch ""([^""]*)"" holds (\d+) of item ""([^""]*)""")]
    public Task GivenBranchHolds(string branchCode, int quantity, string sku) =>
        context.Stock.AddAsync(new InventoryStock(new BranchCode(branchCode), new Sku(sku), quantity));

    // ------------------------------------------------------------------ when

    [When(@"the clerk looks up item ""([^""]*)"" at branch ""([^""]*)""")]
    public async Task WhenTheClerkLooksUp(string sku, string branchCode)
    {
        try
        {
            context.LastStock = await context.CreateService().GetStockAsync(branchCode, sku);
        }
        catch (Exception exception)
        {
            context.LastError = exception;
        }
    }

    [When(@"stock of ""([^""]*)"" at ""([^""]*)"" is adjusted by (-?\d+)")]
    public Task WhenStockIsAdjusted(string sku, string branchCode, int change) =>
        Adjust(sku, branchCode, change, reason: "Stock count correction");

    [When(@"stock of ""([^""]*)"" at ""([^""]*)"" is adjusted by (-?\d+) without a reason")]
    public Task WhenStockIsAdjustedWithoutReason(string sku, string branchCode, int change) =>
        Adjust(sku, branchCode, change, reason: " ");

    private async Task Adjust(string sku, string branchCode, int change, string reason)
    {
        try
        {
            await context.CreateService().AdjustQuantityAsync(branchCode, sku, change, reason, "clerk-1");
        }
        catch (Exception exception)
        {
            context.LastError = exception;
        }
    }

    // ------------------------------------------------------------------ then

    [Then(@"the quantity shown is (\d+)")]
    public void ThenTheQuantityShownIs(int quantity)
    {
        Assert.Null(context.LastError);
        Assert.NotNull(context.LastStock);
        Assert.Equal(quantity, context.LastStock.Quantity);
    }

    [Then("no stock record is shown")]
    public void ThenNoStockRecordIsShown()
    {
        Assert.Null(context.LastError);
        Assert.Null(context.LastStock);
    }

    [Then(@"the quantity of ""([^""]*)"" at ""([^""]*)"" is (\d+)")]
    public async Task ThenTheQuantityIs(string sku, string branchCode, int quantity)
    {
        var stock = await context.Stock.GetByBranchAndSkuAsync(branchCode, sku);
        Assert.NotNull(stock);
        Assert.Equal(quantity, stock.Quantity);
    }

    [Then(@"(\d+) low-stock alerts? was raised")]
    public void ThenLowStockAlertsWereRaised(int count) => Assert.Equal(count, context.Notifications.LowStockAlerts.Count);

    [Then("no low-stock alert was raised")]
    public void ThenNoLowStockAlertWasRaised() => Assert.Empty(context.Notifications.LowStockAlerts);

    [Then("the adjustment is refused because there is not enough stock")]
    public void ThenRefusedNotEnoughStock()
    {
        var error = Assert.IsType<InvalidOperationException>(context.LastError);
        Assert.Contains("Insufficient stock", error.Message, StringComparison.Ordinal);
    }

    [Then("the adjustment is refused as invalid")]
    public void ThenAdjustmentRefusedAsInvalid() => Assert.IsAssignableFrom<ArgumentException>(context.LastError);

    [Then("the lookup is refused as invalid")]
    public void ThenLookupRefusedAsInvalid() => Assert.IsAssignableFrom<ArgumentException>(context.LastError);
}
