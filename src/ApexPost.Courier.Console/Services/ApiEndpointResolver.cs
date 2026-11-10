using Microsoft.Extensions.Options;

namespace ApexPost.Courier.Console.Services;

/// <summary>Which environment the app is running against.</summary>
public enum ApiPlatform
{
    /// <summary>Pick from the OS: Android emulator on Android, Windows host otherwise.</summary>
    Auto,
    Windows,
    AndroidEmulator
}

/// <summary>Configuration section <c>Api</c>.</summary>
public sealed class ApiOptions
{
    /// <summary>Fallback used when no platform specific URL is configured.</summary>
    public string BaseUrl { get; set; } = "https://localhost:54402";

    /// <summary>API address when the app runs on the same Windows machine as the API.</summary>
    public string WindowsBaseUrl { get; set; } = "https://localhost:54402";

    /// <summary>
    /// API address from inside the Android emulator. The emulator's own "localhost" is the emulator,
    /// the host machine is reachable as 10.0.2.2.
    /// </summary>
    public string AndroidEmulatorBaseUrl { get; set; } = "https://10.0.2.2:54402";

    public ApiPlatform Platform { get; set; } = ApiPlatform.Auto;
}

public interface IApiEndpointResolver
{
    Uri BaseAddress { get; }
}

public sealed class ApiEndpointResolver : IApiEndpointResolver
{
    public Uri BaseAddress { get; }

    public ApiEndpointResolver(IOptions<ApiOptions> options)
    {
        var o = options.Value;
        var platform = o.Platform == ApiPlatform.Auto
            ? (OperatingSystem.IsAndroid() ? ApiPlatform.AndroidEmulator : ApiPlatform.Windows)
            : o.Platform;

        var url = platform switch
        {
            ApiPlatform.AndroidEmulator => o.AndroidEmulatorBaseUrl,
            ApiPlatform.Windows => o.WindowsBaseUrl,
            _ => o.BaseUrl
        };

        BaseAddress = new Uri(string.IsNullOrWhiteSpace(url) ? o.BaseUrl : url, UriKind.Absolute);
    }
}
