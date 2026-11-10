using System.Text.Json.Serialization;

namespace ApexPost.Api.DTOs.v2;

/// <summary>
/// v2 Paged response wrapper.
/// </summary>
public sealed class V2PagedResponse<T>
{
    /// <summary>
    /// List of items for the current page.
    /// </summary>
    [JsonPropertyName("items")]
    public IReadOnlyList<T> Items { get; init; } = [];

    /// <summary>
    /// Current page number (1-based).
    /// </summary>
    [JsonPropertyName("page")]
    public int Page { get; init; }

    /// <summary>
    /// Number of items per page.
    /// </summary>
    [JsonPropertyName("pageSize")]
    public int PageSize { get; init; }

    /// <summary>
    /// Total number of items across all pages.
    /// </summary>
    [JsonPropertyName("totalCount")]
    public int TotalCount { get; init; }

    /// <summary>
    /// Total number of pages.
    /// </summary>
    [JsonPropertyName("totalPages")]
    public int TotalPages => PageSize > 0 ? (int)Math.Ceiling((double)TotalCount / PageSize) : 0;
}

/// <summary>
/// v2 Scan upload response with counts.
/// </summary>
public sealed class V2ScanUploadResponse
{
    /// <summary>
    /// Number of scans successfully accepted and stored.
    /// </summary>
    [JsonPropertyName("accepted")]
    public int Accepted { get; init; }

    /// <summary>
    /// Number of scans skipped because they already existed (idempotent).
    /// </summary>
    [JsonPropertyName("duplicatesSkipped")]
    public int DuplicatesSkipped { get; init; }

    /// <summary>
    /// Number of scans rejected with reasons.
    /// </summary>
    [JsonPropertyName("rejected")]
    public int Rejected { get; init; }

    /// <summary>
    /// Details for rejected scans.
    /// </summary>
    [JsonPropertyName("rejectionDetails")]
    public IReadOnlyList<V2ScanRejectionDetail> RejectionDetails { get; init; } = [];

    /// <summary>
    /// Total scans in the request.
    /// </summary>
    [JsonPropertyName("totalProcessed")]
    public int TotalProcessed => Accepted + DuplicatesSkipped + Rejected;
}

/// <summary>
/// v2 Details for a rejected scan.
/// </summary>
public sealed class V2ScanRejectionDetail
{
    /// <summary>
    /// The scan ID that was rejected.
    /// </summary>
    [JsonPropertyName("scanId")]
    public string ScanId { get; init; } = string.Empty;

    /// <summary>
    /// The reason for rejection.
    /// </summary>
    [JsonPropertyName("reason")]
    public string Reason { get; init; } = string.Empty;
}