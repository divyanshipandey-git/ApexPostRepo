using System.Text.Json.Serialization;

namespace ApexPost.Courier.Console.Models;

// These types describe the wire format of the ApexPost v2 API as the courier app sees it.
// They are deliberately owned by this project: the console app must not reference the server
// project (ApexPost.Api) or ASP.NET Core types. The shapes match contracts/v2.json.

/// <summary>Body of POST /api/v2/shipments/{trackingNumber}/scans.</summary>
public sealed class ScanUploadRequestDto
{
    [JsonPropertyName("scans")]
    public List<ScanDocumentDto> Scans { get; init; } = [];
}

public sealed class ScanDocumentDto
{
    [JsonPropertyName("scanId")]
    public string ScanId { get; init; } = string.Empty;

    [JsonPropertyName("scanType")]
    public ScanType ScanType { get; init; }

    [JsonPropertyName("courierId")]
    public string CourierId { get; init; } = string.Empty;

    [JsonPropertyName("location")]
    public string Location { get; init; } = string.Empty;

    [JsonPropertyName("scannedAtUtc")]
    public DateTimeOffset ScannedAtUtc { get; init; }

    [JsonPropertyName("notes")]
    public string? Notes { get; init; }

    [JsonPropertyName("gpsCoordinates")]
    public GpsCoordinatesDto? GpsCoordinates { get; init; }

    [JsonPropertyName("recipientSignature")]
    public string? RecipientSignature { get; init; }
}

public sealed class GpsCoordinatesDto
{
    [JsonPropertyName("latitude")]
    public double Latitude { get; init; }

    [JsonPropertyName("longitude")]
    public double Longitude { get; init; }
}

/// <summary>Response of the scan upload.</summary>
public sealed class ScanUploadResultDto
{
    [JsonPropertyName("accepted")]
    public int Accepted { get; init; }

    [JsonPropertyName("duplicatesSkipped")]
    public int DuplicatesSkipped { get; init; }

    [JsonPropertyName("duplicateScanIds")]
    public List<string> DuplicateScanIds { get; init; } = [];

    [JsonPropertyName("rejected")]
    public int Rejected { get; init; }

    [JsonPropertyName("rejectionDetails")]
    public List<ScanRejectionDto> RejectionDetails { get; init; } = [];
}

public sealed class ScanRejectionDto
{
    [JsonPropertyName("scanId")]
    public string ScanId { get; init; } = string.Empty;

    [JsonPropertyName("reason")]
    public string Reason { get; init; } = string.Empty;
}

/// <summary>RFC 9457 problem details (also covers ValidationProblemDetails via <c>errors</c>).</summary>
public sealed class ProblemDetailsDto
{
    [JsonPropertyName("type")]
    public string? Type { get; init; }

    [JsonPropertyName("title")]
    public string? Title { get; init; }

    [JsonPropertyName("status")]
    public int? Status { get; init; }

    [JsonPropertyName("detail")]
    public string? Detail { get; init; }

    [JsonPropertyName("instance")]
    public string? Instance { get; init; }

    [JsonPropertyName("errors")]
    public Dictionary<string, string[]>? Errors { get; init; }
}

/// <summary>Body of POST /dev/token (Development only).</summary>
public sealed class DevTokenRequestDto
{
    [JsonPropertyName("courierId")]
    public string CourierId { get; init; } = string.Empty;

    [JsonPropertyName("role")]
    public string Role { get; init; } = "Courier";
}

public sealed class DevTokenResponseDto
{
    [JsonPropertyName("access_token")]
    public string AccessToken { get; init; } = string.Empty;

    [JsonPropertyName("token_type")]
    public string? TokenType { get; init; }

    [JsonPropertyName("expires_in")]
    public int ExpiresIn { get; init; }
}
