using System.Globalization;
using ApexPost.Domain.Entities;
using ApexPost.Domain.ValueObjects;
using ApexPost.Specs.Support;
using Reqnroll;
using Xunit;

namespace ApexPost.Specs.StepDefinitions;

/// <summary>Steps of MoneyOrders.feature. SERVICE level: the real MoneyOrderService and policy, in-memory store, no HTTP.</summary>
[Binding]
public sealed class MoneyOrderSteps(MoneyOrderScenarioContext context)
{
    private const string Sender = "Asha Sharma";
    private const string Recipient = "Ravi Kumar";

    private static decimal Amount(string text) => decimal.Parse(text, CultureInfo.InvariantCulture);

    // ------------------------------------------------------------------ given

    [Given(@"the identity verification limit is (\d+) rupees")]
    public void GivenTheIdentityVerificationLimitIs(int limit) => context.VerificationLimit = limit;

    [Given(@"the clerk works at branch ""([^""]*)"" on transaction ""([^""]*)""")]
    public void GivenTheClerkWorksAt(string branchCode, string transactionId)
    {
        context.BranchCode = branchCode;
        context.TransactionId = transactionId;
    }

    // ------------------------------------------------------------------ when

    [When(@"the clerk issues money order ""([^""]*)"" for (-?\d+(?:\.\d+)?) rupees")]
    public Task WhenTheClerkIssuesWithoutCheck(string id, string amount) =>
        Issue(id, amount, identity: null);

    [When(@"the clerk issues money order ""([^""]*)"" for (-?\d+(?:\.\d+)?) rupees after a failed identity check")]
    public Task WhenTheClerkIssuesAfterFailedCheck(string id, string amount) =>
        Issue(id, amount, new IdentityVerification("IV-" + id, "Passport", "XXXX0000", isVerified: false, context.Clock.Now));

    [When(@"the clerk issues money order ""([^""]*)"" for (-?\d+(?:\.\d+)?) rupees after a verified identity check on document ""([^""]*)""")]
    public Task WhenTheClerkIssuesAfterVerifiedCheck(string id, string amount, string maskedDocument) =>
        Issue(id, amount, new IdentityVerification("IV-" + id, "Passport", maskedDocument, isVerified: true, context.Clock.Now));

    private async Task Issue(string id, string amount, IdentityVerification? identity)
    {
        try
        {
            await context.CreateService().CreateAsync(
                id,
                new Money(Amount(amount)),
                Sender,
                Recipient,
                new Money(10m),
                identity,
                new BranchCode(context.BranchCode),
                context.TransactionId);
        }
        catch (Exception exception)
        {
            context.LastError = exception;
        }
    }

    // ------------------------------------------------------------------ then

    [Then(@"money order ""([^""]*)"" is stored with amount (\d+(?:\.\d+)?) rupees")]
    public async Task ThenMoneyOrderIsStoredWithAmount(string id, string amount)
    {
        Assert.Null(context.LastError);
        var stored = await context.Orders.GetByIdAsync(id);
        Assert.NotNull(stored);
        Assert.Equal(Amount(amount), stored.Amount.Amount);
    }

    [Then(@"the stored order is stamped with branch ""([^""]*)"" and transaction ""([^""]*)""")]
    public async Task ThenTheStoredOrderIsStamped(string branchCode, string transactionId)
    {
        var stored = Assert.Single(await context.Orders.GetByBranchAsync(branchCode));
        Assert.Equal(branchCode, stored.BranchCode);
        Assert.Equal(transactionId, stored.TransactionId);
        Assert.Equal(context.Clock.Now, stored.CreatedAtUtc);
    }

    [Then("the order was saved exactly once")]
    public void ThenTheOrderWasSavedExactlyOnce() => Assert.Equal(1, context.UnitOfWork.SaveCount);

    [Then("the order was never saved")]
    public void ThenTheOrderWasNeverSaved() => Assert.Equal(0, context.UnitOfWork.SaveCount);

    [Then("the issue is refused because identity verification is required")]
    public void ThenRefusedBecauseVerificationRequired()
    {
        var error = Assert.IsType<InvalidOperationException>(context.LastError);
        Assert.Contains("identity verification", error.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Then("the issue is refused because the amount must be greater than zero")]
    public void ThenRefusedBecauseAmountNotPositive()
    {
        var error = Assert.IsType<ArgumentOutOfRangeException>(context.LastError);
        Assert.Contains("greater than zero", error.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Then("no money order is stored")]
    public void ThenNoMoneyOrderIsStored() => Assert.Equal(0, context.Orders.Count);

    [Then(@"the stored order keeps the masked document number ""([^""]*)"" marked as verified")]
    public async Task ThenTheStoredOrderKeepsTheMaskedNumber(string maskedDocument)
    {
        var stored = Assert.Single(await context.Orders.GetByBranchAsync(context.BranchCode));
        Assert.NotNull(stored.IdentityVerification);
        Assert.Equal(maskedDocument, stored.IdentityVerification.MaskedDocumentNumber);
        Assert.True(stored.IdentityVerification.IsVerified);
    }
}
