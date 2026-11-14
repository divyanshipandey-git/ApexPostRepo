using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using ApexPost.IntegrationTests.TestSupport;
using Xunit;

namespace ApexPost.IntegrationTests.Api;

/// <summary>
/// End to end through the real ApexPost.Api pipeline (routing, [ApiController] validation, JWT bearer validation,
/// role policies, exception handler, EF Core repositories) with in-memory SQLite. A fresh host and database per test.
/// Not proven here: SQL Server behaviour, real TLS, real identity provider.
/// </summary>
public sealed class ShipmentApiEndpointTests : IDisposable
{
    private const string Tracking = "APX-20250101";
    private readonly ApexApiFactory _factory = new();

    public void Dispose() => _factory.Dispose();

    private HttpClient ClientFor(string? token)
    {
        var client = _factory.CreateClient();
        if (token is not null)
        {
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        }

        return client;
    }

    private static object NewShipment(string tracking = Tracking, double weight = 2.5) => new
    {
        trackingNumber = tracking,
        weightKg = weight,
        recipientName = "Jane Example",
        recipientPhone = "+15550100",
        recipientAddress = "1 Example Street",
        recipientCity = "Springfield"
    };

    private async Task CreateShipmentAsAdminAsync(string tracking = Tracking)
    {
        var response = await ClientFor(TestJwt.Admin()).PostAsJsonAsync("/api/v2/shipments", NewShipment(tracking));
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
    }

    private static async Task<JsonElement> JsonOf(HttpResponseMessage response) =>
        JsonDocument.Parse(await response.Content.ReadAsStringAsync()).RootElement.Clone();

    // ------------------------------------------------------------------ authentication and roles (risk R4)

    [Fact]
    public async Task PostShipment_WithoutToken_Returns401ProblemDetails()
    {
        // Act
        var response = await ClientFor(null).PostAsJsonAsync("/api/v2/shipments", NewShipment());

        // Assert
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
        Assert.Equal(401, (await JsonOf(response)).GetProperty("status").GetInt32());
    }

    [Fact]
    public async Task PostShipment_WithExpiredToken_Returns401()
    {
        // Arrange
        var expired = TestJwt.Create("ADMIN-001", "Admin", expires: DateTime.UtcNow.AddHours(-1));

        // Act
        var response = await ClientFor(expired).PostAsJsonAsync("/api/v2/shipments", NewShipment());

        // Assert
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task PostShipment_WithTokenSignedByAnotherKey_Returns401()
    {
        // Arrange
        var forged = TestJwt.Create("ADMIN-001", "Admin", signingKey: "a-completely-different-key-0123456789-abcdef");

        // Act
        var response = await ClientFor(forged).PostAsJsonAsync("/api/v2/shipments", NewShipment());

        // Assert
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Theory]
    [InlineData("/api/v1/shipments")]
    [InlineData("/api/v2/shipments")]
    public async Task PostShipment_AsCourier_Returns403ForBothVersions(string route)
    {
        // Act
        var response = await ClientFor(TestJwt.Courier()).PostAsJsonAsync(route, NewShipment());

        // Assert
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        Assert.Equal(403, (await JsonOf(response)).GetProperty("status").GetInt32());
    }

    [Fact]
    public async Task DeleteShipment_AsCourier_Returns403AndTheShipmentSurvives()
    {
        // Arrange
        await CreateShipmentAsAdminAsync();

        // Act
        var response = await ClientFor(TestJwt.Courier()).DeleteAsync($"/api/v2/shipments/{Tracking}");

        // Assert
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await ClientFor(TestJwt.Courier()).GetAsync($"/api/v2/shipments/{Tracking}")).StatusCode);
    }

    // ------------------------------------------------------------------ create, read, duplicate, validation (R2, R7)

    [Fact]
    public async Task PostShipment_AsAdmin_Returns201WithLocationHeaderAndBody()
    {
        // Act
        var response = await ClientFor(TestJwt.Admin()).PostAsJsonAsync("/api/v2/shipments", NewShipment());

        // Assert
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        Assert.EndsWith($"/api/v2/shipments/{Tracking}", response.Headers.Location!.ToString());
        var body = await JsonOf(response);
        Assert.Equal(Tracking, body.GetProperty("trackingNumber").GetString());
        Assert.Equal("Created", body.GetProperty("status").GetString());
        Assert.Equal("Jane Example", body.GetProperty("recipient").GetProperty("name").GetString());
    }

    [Fact]
    public async Task GetShipment_LocationHeaderOfThePost_ReturnsTheSameShipment()
    {
        // Arrange
        var created = await ClientFor(TestJwt.Admin()).PostAsJsonAsync("/api/v2/shipments", NewShipment());

        // Act
        var response = await ClientFor(TestJwt.Courier()).GetAsync(created.Headers.Location);

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(Tracking, (await JsonOf(response)).GetProperty("trackingNumber").GetString());
    }

    [Fact]
    public async Task PostShipment_DuplicateTrackingNumber_Returns409ProblemDetails()
    {
        // Arrange
        await CreateShipmentAsAdminAsync();

        // Act
        var response = await ClientFor(TestJwt.Admin()).PostAsJsonAsync("/api/v2/shipments", NewShipment());

        // Assert
        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.Equal(409, (await JsonOf(response)).GetProperty("status").GetInt32());
    }

    [Theory]
    [InlineData("APX-1234", 2.5, "trackingNumber")]
    [InlineData("APX-20250101", 0.05, "weightKg")]
    [InlineData("APX-20250101", 70.5, "weightKg")]
    public async Task PostShipment_InvalidInput_Returns400ValidationProblemNamingTheField(string tracking, double weight, string field)
    {
        // Act
        var response = await ClientFor(TestJwt.Admin()).PostAsJsonAsync("/api/v2/shipments", NewShipment(tracking, weight));

        // Assert
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var errors = (await JsonOf(response)).GetProperty("errors");
        Assert.Contains(errors.EnumerateObject(), p => string.Equals(p.Name, field, StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task PostShipment_WrongContentType_Returns415()
    {
        // Arrange
        using var content = new StringContent("trackingNumber=APX-20250101", Encoding.UTF8, "text/plain");

        // Act
        var response = await ClientFor(TestJwt.Admin()).PostAsync("/api/v2/shipments", content);

        // Assert
        Assert.Equal(HttpStatusCode.UnsupportedMediaType, response.StatusCode);
    }

    [Fact]
    public async Task GetShipment_UnknownTrackingNumber_Returns404ProblemDetails()
    {
        // Act
        var response = await ClientFor(TestJwt.Courier()).GetAsync("/api/v2/shipments/APX-20259999");

        // Assert
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Equal(404, (await JsonOf(response)).GetProperty("status").GetInt32());
    }

    [Theory]
    [InlineData(2.5, 0.0)]
    [InlineData(30.0, 0.0)]
    [InlineData(40.0, 20.0)]
    [InlineData(70.0, 42.5)]
    public async Task PostShipment_V2_ReportsTheHeavyParcelSurcharge(double weight, double expectedSurcharge)
    {
        // Act
        var response = await ClientFor(TestJwt.Admin()).PostAsJsonAsync("/api/v2/shipments", NewShipment(weight: weight));
        var created = await JsonOf(response);
        var fetched = await JsonOf(await ClientFor(TestJwt.Courier()).GetAsync($"/api/v2/shipments/{Tracking}"));

        // Assert: the amount is on the create response and on the later GET.
        Assert.Equal(expectedSurcharge, created.GetProperty("surchargeAmount").GetDouble());
        Assert.Equal(expectedSurcharge, fetched.GetProperty("surchargeAmount").GetDouble());
    }

    [Fact]
    public async Task GetShipment_V1_KeepsItsContractAndHasNoSurchargeField()
    {
        // Arrange
        await ClientFor(TestJwt.Admin()).PostAsJsonAsync("/api/v1/shipments", NewShipment(weight: 40));

        // Act
        var v1 = await JsonOf(await ClientFor(TestJwt.Courier()).GetAsync($"/api/v1/shipments/{Tracking}"));

        // Assert: v1 is frozen and deprecated; the new field is additive in v2 only.
        Assert.False(v1.TryGetProperty("surchargeAmount", out _));
    }

    [Fact]
    public async Task GetShipment_V1AndV2_ReturnDifferentShapesFromTheSameData()
    {
        // Arrange
        await CreateShipmentAsAdminAsync();
        var client = ClientFor(TestJwt.Courier());

        // Act
        var v1 = await JsonOf(await client.GetAsync($"/api/v1/shipments/{Tracking}"));
        var v2 = await JsonOf(await client.GetAsync($"/api/v2/shipments/{Tracking}"));

        // Assert: v1 is flat and deprecated, v2 nests the recipient.
        Assert.Equal("Springfield", v1.GetProperty("recipientCity").GetString());
        Assert.False(v1.TryGetProperty("recipient", out _));
        Assert.Equal("Springfield", v2.GetProperty("recipient").GetProperty("city").GetString());
    }

    [Fact]
    public async Task GetShipments_WithPaging_ReturnsTheRequestedSliceAndTotal()
    {
        // Arrange
        await CreateShipmentAsAdminAsync("APX-20250101");
        await CreateShipmentAsAdminAsync("APX-20250102");
        await CreateShipmentAsAdminAsync("APX-20250103");

        // Act
        var response = await ClientFor(TestJwt.Courier()).GetAsync("/api/v2/shipments?page=1&pageSize=2");
        var body = await JsonOf(response);

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(2, body.GetProperty("items").GetArrayLength());
        Assert.Equal(3, body.GetProperty("totalCount").GetInt32());
    }

    // ------------------------------------------------------------------ status changes (R1)

    [Fact]
    public async Task PatchStatus_LegalThenIllegalTransition_Returns204Then409()
    {
        // Arrange
        await CreateShipmentAsAdminAsync();
        var courier = ClientFor(TestJwt.Courier());

        // Act
        var legal = await courier.PatchAsJsonAsync($"/api/v2/shipments/{Tracking}/status", new { status = "PickedUp" });
        var illegal = await courier.PatchAsJsonAsync($"/api/v2/shipments/{Tracking}/status", new { status = "Delivered", reason = "Handed over", recipientSignature = "Jane Example" });

        // Assert: PickedUp cannot jump straight to Delivered.
        Assert.Equal(HttpStatusCode.NoContent, legal.StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, illegal.StatusCode);
    }

    // ------------------------------------------------------------------ scan upload (R3)

    private static object ScanPayload(string scanId, string courierId = "COURIER-001", string scanType = "Pickup") => new
    {
        scans = new[]
        {
            new
            {
                scanId,
                scanType,
                courierId,
                location = "Depot A",
                scannedAtUtc = DateTimeOffset.UtcNow.AddMinutes(-1),
                notes = "ok"
            }
        }
    };

    [Fact]
    public async Task PostScans_SameScanIdTwice_SecondUploadReportsADuplicateAndStoresOnce()
    {
        // Arrange
        await CreateShipmentAsAdminAsync();
        var courier = ClientFor(TestJwt.Courier());
        var payload = ScanPayload("scan::11111111-1111-1111-1111-111111111111");

        // Act
        var first = await JsonOf(await courier.PostAsJsonAsync($"/api/v2/shipments/{Tracking}/scans", payload));
        var second = await JsonOf(await courier.PostAsJsonAsync($"/api/v2/shipments/{Tracking}/scans", payload));
        var history = await JsonOf(await courier.GetAsync($"/api/v2/shipments/{Tracking}/scans"));

        // Assert
        Assert.Equal(1, first.GetProperty("accepted").GetInt32());
        Assert.Equal(0, second.GetProperty("accepted").GetInt32());
        Assert.Equal(1, second.GetProperty("duplicatesSkipped").GetInt32());
        Assert.Equal(1, history.GetArrayLength());
    }

    [Fact]
    public async Task PostScans_ScanOfAnotherCourier_Returns403()
    {
        // Arrange
        await CreateShipmentAsAdminAsync();

        // Act
        var response = await ClientFor(TestJwt.Courier("COURIER-001"))
            .PostAsJsonAsync($"/api/v2/shipments/{Tracking}/scans", ScanPayload("scan::22222222-2222-2222-2222-222222222222", courierId: "COURIER-999"));

        // Assert
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task PostScans_AsAdmin_Returns403BecauseOnlyCouriersMayUpload()
    {
        // Arrange
        await CreateShipmentAsAdminAsync();

        // Act
        var response = await ClientFor(TestJwt.Admin())
            .PostAsJsonAsync($"/api/v2/shipments/{Tracking}/scans", ScanPayload("scan::33333333-3333-3333-3333-333333333333", courierId: "ADMIN-001"));

        // Assert
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task PostScans_DeliveredWithoutSignature_IsRejectedWithAReasonNotAnHttpError()
    {
        // Arrange
        await CreateShipmentAsAdminAsync();

        // Act
        var response = await ClientFor(TestJwt.Courier())
            .PostAsJsonAsync($"/api/v2/shipments/{Tracking}/scans", ScanPayload("scan::44444444-4444-4444-4444-444444444444", scanType: "Delivered"));
        var body = await JsonOf(response);

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(1, body.GetProperty("rejected").GetInt32());
        Assert.Contains("signature", body.GetProperty("rejectionDetails")[0].GetProperty("reason").GetString());
    }

    // ------------------------------------------------------------------ delete rule

    [Fact]
    public async Task DeleteShipment_AsAdminWhileCreated_Returns204ThenGetReturns404()
    {
        // Arrange
        await CreateShipmentAsAdminAsync();
        var admin = ClientFor(TestJwt.Admin());

        // Act
        var deleted = await admin.DeleteAsync($"/api/v2/shipments/{Tracking}");
        var after = await admin.GetAsync($"/api/v2/shipments/{Tracking}");

        // Assert
        Assert.Equal(HttpStatusCode.NoContent, deleted.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, after.StatusCode);
    }

    [Fact]
    public async Task DeleteShipment_AfterPickup_Returns409()
    {
        // Arrange
        await CreateShipmentAsAdminAsync();
        await ClientFor(TestJwt.Courier()).PatchAsJsonAsync($"/api/v2/shipments/{Tracking}/status", new { status = "PickedUp" });

        // Act
        var response = await ClientFor(TestJwt.Admin()).DeleteAsync($"/api/v2/shipments/{Tracking}");

        // Assert
        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    }
}
