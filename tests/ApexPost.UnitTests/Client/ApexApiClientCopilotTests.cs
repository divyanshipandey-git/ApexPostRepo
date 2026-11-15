// COPILOT-ASSISTED: drafted with GitHub Copilot (prompt 3 in COPILOT-LOG.md), then reviewed by the student.
using System.Net;
using ApexPost.Courier.Console.Models;
using ApexPost.Courier.Console.Services;
using ApexPost.UnitTests.TestSupport;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace ApexPost.UnitTests.Client;

public sealed class ApexApiClientCopilotTests
{
    private const string Tracking = "APX-20250101";

    private static ApexApiClient CreateClient(StubHttpMessageHandler handler) =>
        new(new HttpClient(handler) { BaseAddress = new Uri("https://api.test.invalid/") }, NullLogger<ApexApiClient>.Instance);

    private static ScanUploadRequestDto Request() => new()
    {
        Scans =
        [
            new ScanDocumentDto
            {
                ScanId = "scan::11111111-1111-1111-1111-111111111111",
                ScanType = ScanType.Pickup,
                CourierId = "COURIER-001",
                Location = "Depot A",
                ScannedAtUtc = new DateTimeOffset(2025, 1, 1, 9, 0, 0, TimeSpan.Zero)
            }
        ]
    };

    [Fact]
    public async Task UploadScansAsync_200WithJson_ReturnsDeserializedResponse()
    {
        // Arrange
        var handler = StubHttpMessageHandler.Json(HttpStatusCode.OK,
            """{"accepted":2,"duplicatesSkipped":1,"duplicateScanIds":["scan::dup"],"rejected":1,"rejectionDetails":[{"scanId":"scan::bad","reason":"Scan time cannot be in the future"}]}""");
        var client = CreateClient(handler);

        // Act
        var result = await client.UploadScansAsync(Tracking, Request());

        // Assert
        Assert.NotNull(result);
        Assert.Equal(2, result!.Accepted);
        Assert.Equal(1, result.DuplicatesSkipped);
        Assert.Equal(["scan::dup"], result.DuplicateScanIds);
        Assert.Equal(1, result.Rejected);
        Assert.Equal("Scan time cannot be in the future", Assert.Single(result.RejectionDetails).Reason);
    }

    [Fact]
    public async Task UploadScansAsync_404WithProblemDetails_ThrowsApiExceptionWithProblemInfo()
    {
        // Arrange
        var handler = StubHttpMessageHandler.Json(HttpStatusCode.NotFound,
            """{"type":"https://tools.ietf.org/html/rfc9110#section-15.5.5","title":"Resource Not Found","status":404,"detail":"Shipment APX-20250101 not found"}""",
            "application/problem+json");
        var client = CreateClient(handler);

        // Act
        var ex = await Assert.ThrowsAsync<ApiException>(() => client.UploadScansAsync(Tracking, Request()));

        // Assert
        Assert.Equal(404, ex.StatusCode);
        Assert.Equal("Resource Not Found", ex.Message);
        Assert.Equal("Shipment APX-20250101 not found", ex.Detail);
        Assert.Contains("rfc9110", ex.ProblemType);
    }

    [Fact]
    public async Task UploadScansAsync_500WithPlainText_ThrowsApiExceptionWithRawBodyAsDetail()
    {
        // Arrange
        var client = CreateClient(StubHttpMessageHandler.Json(HttpStatusCode.InternalServerError, "boom", "text/plain"));

        // Act
        var ex = await Assert.ThrowsAsync<ApiException>(() => client.UploadScansAsync(Tracking, Request()));

        // Assert
        Assert.Equal(500, ex.StatusCode);
        Assert.Equal("boom", ex.Detail);
        Assert.Contains("InternalServerError", ex.Message);
    }

    [Fact]
    public async Task UploadScansAsync_HttpClientTimeout_PropagatesTaskCanceledException()
    {
        // Arrange
        var handler = StubHttpMessageHandler.Throwing(new TaskCanceledException("timed out", new TimeoutException()));
        var client = CreateClient(handler);

        // Act
        var ex = await Assert.ThrowsAsync<TaskCanceledException>(() => client.UploadScansAsync(Tracking, Request()));

        // Assert
        Assert.IsType<TimeoutException>(ex.InnerException);
    }

    [Fact]
    public async Task UploadScansAsync_CallerCancellation_ThrowsOperationCanceledException()
    {
        // Arrange
        var handler = new StubHttpMessageHandler(async (_, token) =>
        {
            await Task.Delay(Timeout.Infinite, token);
            return new HttpResponseMessage(HttpStatusCode.OK);
        });
        var client = CreateClient(handler);
        using var cts = new CancellationTokenSource();
        var call = client.UploadScansAsync(Tracking, Request(), cts.Token);

        // Act
        await cts.CancelAsync();

        // Assert
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => call);
    }

    // Added in review: Copilot never looked at the REQUEST, so a client posting to the wrong route or with the wrong
    // JSON names would still pass every test above.
    [Fact]
    public async Task UploadScansAsync_AnyCall_PostsToTheV2ScansRouteWithCamelCaseJson()
    {
        // Arrange
        var handler = StubHttpMessageHandler.Json(HttpStatusCode.OK, """{"accepted":1}""");
        var client = CreateClient(handler);

        // Act
        await client.UploadScansAsync(Tracking, Request());

        // Assert
        var sent = Assert.Single(handler.Requests);
        Assert.Equal(HttpMethod.Post, sent.Method);
        Assert.Equal($"https://api.test.invalid/api/v2/shipments/{Tracking}/scans", sent.Uri!.ToString());
        Assert.Contains("\"scanId\":\"scan::11111111-1111-1111-1111-111111111111\"", sent.Body);
    }

    // Added in review: Copilot covered 404 and 500 only; other statuses the courier app meets in the field.
    [Theory]
    [InlineData(HttpStatusCode.BadRequest, 400)]
    [InlineData(HttpStatusCode.Unauthorized, 401)]
    [InlineData(HttpStatusCode.Forbidden, 403)]
    [InlineData(HttpStatusCode.Conflict, 409)]
    [InlineData(HttpStatusCode.ServiceUnavailable, 503)]
    public async Task UploadScansAsync_ErrorStatus_ThrowsApiExceptionCarryingTheStatusCode(HttpStatusCode status, int expected)
    {
        // Arrange
        var client = CreateClient(StubHttpMessageHandler.Json(status, "{}"));

        // Act
        var ex = await Assert.ThrowsAsync<ApiException>(() => client.UploadScansAsync(Tracking, Request()));

        // Assert
        Assert.Equal(expected, ex.StatusCode);
    }

    [Fact]
    public async Task UploadScansAsync_200WithMalformedJson_ThrowsJsonException()
    {
        // Arrange
        var client = CreateClient(StubHttpMessageHandler.Json(HttpStatusCode.OK, "{ this is not json"));

        // Act + Assert
        await Assert.ThrowsAnyAsync<System.Text.Json.JsonException>(() => client.UploadScansAsync(Tracking, Request()));
    }
}
