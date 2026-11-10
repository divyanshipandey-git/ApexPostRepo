using System.Text.Json;
using System.Text.Json.Serialization;

namespace ApexPost.Courier.Console.Models;

/// <summary>
/// Source-generated JSON metadata for every type the courier app serializes. No reflection-based
/// serialization is used for API traffic or token storage, which keeps the app trim/AOT friendly
/// and removes the startup reflection cost.
/// </summary>
[JsonSourceGenerationOptions(
    PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
    PropertyNameCaseInsensitive = true,
    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull)]
[JsonSerializable(typeof(ScanUploadRequestDto))]
[JsonSerializable(typeof(ScanUploadResultDto))]
[JsonSerializable(typeof(ProblemDetailsDto))]
[JsonSerializable(typeof(DevTokenRequestDto))]
[JsonSerializable(typeof(DevTokenResponseDto))]
[JsonSerializable(typeof(TokenData))]
internal sealed partial class ApexJsonContext : JsonSerializerContext
{
}
