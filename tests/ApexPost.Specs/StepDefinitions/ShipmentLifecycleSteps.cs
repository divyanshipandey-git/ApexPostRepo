using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using ApexPost.IntegrationTests.TestSupport;
using ApexPost.Specs.Support;
using Reqnroll;
using Xunit;

namespace ApexPost.Specs.StepDefinitions;

/// <summary>
/// Steps of ShipmentLifecycle.feature and HeavyParcelSurcharge.feature (API level). Shared steps such as
/// "I am signed in as ..." and "the API answers with status ..." live in ShipmentCreationSteps; state is shared
/// through the injected ApiScenarioContext.
/// </summary>
[Binding]
public sealed class ShipmentLifecycleSteps(ApiScenarioContext context)
{
    [Given("shipment {string} weighing {double} kg is already registered")]
    public async Task GivenShipmentWeighingIsAlreadyRegistered(string trackingNumber, double weight)
    {
        var response = await context.CreateClient(TestJwt.Admin()).PostAsJsonAsync("/api/v2/shipments", new
        {
            trackingNumber,
            weightKg = weight,
            recipientName = "Jane Example",
            recipientPhone = "+15550100",
            recipientAddress = "1 Example Street",
            recipientCity = "Springfield"
        });
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
    }

    [Given("shipment {string} has been picked up")]
    public async Task GivenShipmentHasBeenPickedUp(string trackingNumber) =>
        await ChangeStatusAsync(trackingNumber, "PickedUp", expect: HttpStatusCode.NoContent);

    [Given("shipment {string} has been delivered")]
    public async Task GivenShipmentHasBeenDelivered(string trackingNumber)
    {
        await ChangeStatusAsync(trackingNumber, "PickedUp", expect: HttpStatusCode.NoContent);
        await ChangeStatusAsync(trackingNumber, "InTransit", expect: HttpStatusCode.NoContent);
        await ChangeStatusAsync(trackingNumber, "Delivered", expect: HttpStatusCode.NoContent, signature: "Jane Example");
    }

    [When("I change the status of {string} to {string}")]
    public async Task WhenIChangeTheStatus(string trackingNumber, string status) =>
        context.Response = await PatchAsync(trackingNumber, status, signature: null);

    [When("I deliver shipment {string} signed by {string}")]
    public async Task WhenIDeliverShipment(string trackingNumber, string signature) =>
        context.Response = await PatchAsync(trackingNumber, "Delivered", signature);

    [When("I delete shipment {string}")]
    public async Task WhenIDeleteShipment(string trackingNumber) =>
        context.Response = await context.CreateClient().DeleteAsync($"/api/v2/shipments/{trackingNumber}");

    [When("I read shipment {string}")]
    public async Task WhenIReadShipment(string trackingNumber)
    {
        context.Response = await context.CreateClient().GetAsync($"/api/v2/shipments/{trackingNumber}");
        var text = await context.Response.Content.ReadAsStringAsync();
        context.Body = string.IsNullOrWhiteSpace(text) ? null : JsonDocument.Parse(text).RootElement.Clone();
    }

    [Then("the surcharge shown is {double}")]
    public async Task ThenTheSurchargeShownIs(double expected)
    {
        if (context.Body is null)
        {
            var text = await context.Response!.Content.ReadAsStringAsync();
            context.Body = JsonDocument.Parse(text).RootElement.Clone();
        }

        Assert.Equal((decimal)expected, context.Body.Value.GetProperty("surchargeAmount").GetDecimal());
    }

    private Task<HttpResponseMessage> PatchAsync(string trackingNumber, string status, string? signature) =>
        context.CreateClient().PatchAsJsonAsync($"/api/v2/shipments/{trackingNumber}/status", new
        {
            status,
            reason = signature is null ? null : "Handed over",
            recipientSignature = signature
        });

    private async Task ChangeStatusAsync(string trackingNumber, string status, HttpStatusCode expect, string? signature = null)
    {
        // Setup steps always act as a courier, whoever the scenario is signed in as afterwards.
        var response = await context.CreateClient(TestJwt.Courier()).PatchAsJsonAsync($"/api/v2/shipments/{trackingNumber}/status", new
        {
            status,
            reason = signature is null ? null : "Handed over",
            recipientSignature = signature
        });
        Assert.Equal(expect, response.StatusCode);
    }
}
