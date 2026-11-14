using System.Net.Http.Json;
using System.Text.Json;
using ApexPost.IntegrationTests.TestSupport;
using ApexPost.Specs.Support;
using Reqnroll;
using Xunit;

namespace ApexPost.Specs.StepDefinitions;

/// <summary>Steps of ShipmentCreation.feature. Runs at API level: real HTTP pipeline, in-memory host, SQLite.</summary>
[Binding]
public sealed class ShipmentCreationSteps(ApiScenarioContext context)
{
    private const string Admin = "admin";

    [Given("the ApexPost API is running with an empty database")]
    public async Task GivenTheApiIsRunningWithAnEmptyDatabase()
    {
        // The @api hook already started the host; here we prove the premise of the scenario.
        Assert.Equal(0, await CountShipmentsAsync());
    }

    [Given("I am signed in as an administrator")]
    public void GivenIAmSignedInAsAnAdministrator() => context.Token = TestJwt.Admin();

    [Given("I am signed in as a courier")]
    public void GivenIAmSignedInAsACourier() => context.Token = TestJwt.Courier();

    [Given("shipment {string} is already registered")]
    public async Task GivenShipmentIsAlreadyRegistered(string trackingNumber)
    {
        var response = await context.CreateClient(TestJwt.Admin()).PostAsJsonAsync("/api/v2/shipments",
            Payload(trackingNumber, 2.5, "Jane Example", "Springfield"));
        Assert.Equal(System.Net.HttpStatusCode.Created, response.StatusCode);
    }

    [When("I register shipment {string} weighing {double} kg for {string} in {string}")]
    public async Task WhenIRegisterShipment(string trackingNumber, double weight, string recipient, string city)
    {
        context.Response = await context.CreateClient().PostAsJsonAsync("/api/v2/shipments", Payload(trackingNumber, weight, recipient, city));
        await CaptureBodyAsync();
    }

    [Then("the API answers with status {int}")]
    public void ThenTheApiAnswersWithStatus(int status) => Assert.Equal(status, (int)context.Response!.StatusCode);

    [Then("the Location header ends with {string}")]
    public void ThenTheLocationHeaderEndsWith(string suffix) =>
        Assert.EndsWith(suffix, context.Response!.Headers.Location?.ToString());

    [Then("shipment {string} can be read back with status {string}")]
    public async Task ThenShipmentCanBeReadBack(string trackingNumber, string status)
    {
        var response = await context.CreateClient(TestJwt.Courier()).GetAsync($"/api/v2/shipments/{trackingNumber}");
        response.EnsureSuccessStatusCode();
        var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync()).RootElement;
        Assert.Equal(status, body.GetProperty("status").GetString());
    }

    [Then("the problem title is {string}")]
    public void ThenTheProblemTitleIs(string title) => Assert.Equal(title, context.Body!.Value.GetProperty("title").GetString());

    [Then("the validation errors mention {string}")]
    public void ThenTheValidationErrorsMention(string field)
    {
        var errors = context.Body!.Value.GetProperty("errors");
        Assert.Contains(errors.EnumerateObject(), property => string.Equals(property.Name, field, StringComparison.OrdinalIgnoreCase));
    }

    [Then("only one shipment exists in total")]
    public async Task ThenOnlyOneShipmentExists() => Assert.Equal(1, await CountShipmentsAsync());

    [Then("no shipments exist")]
    public async Task ThenNoShipmentsExist() => Assert.Equal(0, await CountShipmentsAsync());

    private async Task<int> CountShipmentsAsync()
    {
        var response = await context.CreateClient(TestJwt.Courier()).GetAsync("/api/v2/shipments?page=1&pageSize=1");
        response.EnsureSuccessStatusCode();
        return JsonDocument.Parse(await response.Content.ReadAsStringAsync()).RootElement.GetProperty("totalCount").GetInt32();
    }

    private async Task CaptureBodyAsync()
    {
        var text = await context.Response!.Content.ReadAsStringAsync();
        context.Body = string.IsNullOrWhiteSpace(text) ? null : JsonDocument.Parse(text).RootElement.Clone();
    }

    private static object Payload(string trackingNumber, double weight, string recipient, string city) => new
    {
        trackingNumber,
        weightKg = weight,
        recipientName = recipient,
        recipientPhone = "+15550100",
        recipientAddress = "1 Example Street",
        recipientCity = city
    };
}
