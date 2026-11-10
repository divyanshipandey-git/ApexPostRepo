using Asp.Versioning;
using Microsoft.AspNetCore.Http;

namespace ApexPost.Api.Extensions;

/// <summary>
/// Custom API version reader that extracts version from URL segment at a specific index.
/// </summary>
public sealed class CustomUrlSegmentApiVersionReader : IApiVersionReader
{
    private readonly int _segmentIndex;

    public CustomUrlSegmentApiVersionReader(int segmentIndex)
    {
        _segmentIndex = segmentIndex;
    }

    public IReadOnlyList<string> Read(HttpRequest request)
    {
        var path = request.Path.Value;
        if (string.IsNullOrEmpty(path))
        {
            return Array.Empty<string>();
        }

        var segments = path.Split('/', StringSplitOptions.RemoveEmptyEntries);
        if (segments.Length <= _segmentIndex)
        {
            return Array.Empty<string>();
        }

        var versionSegment = segments[_segmentIndex];
        if (versionSegment.StartsWith("v", StringComparison.OrdinalIgnoreCase) &&
            int.TryParse(versionSegment.Substring(1), out var majorVersion))
        {
            return new[] { majorVersion.ToString() };
        }

        return Array.Empty<string>();
    }

    public void AddParameters(IApiVersionParameterDescriptionContext context)
    {
        // No additional parameters to add for URL segment versioning
    }
}