using System.Net.Http.Headers;
using System.Text.Json;
using ApexPost.IntegrationTests.TestSupport;

namespace ApexPost.Specs.Support;

/// <summary>
/// State of ONE API scenario. Reqnroll creates a new instance per scenario and injects it into the
/// binding classes through their constructors (context injection), so nothing is shared through static fields.
/// </summary>
public sealed class ApiScenarioContext : IDisposable
{
    public ApexApiFactory? Factory { get; private set; }

    public string? Token { get; set; }

    public HttpResponseMessage? Response { get; set; }

    public JsonElement? Body { get; set; }

    public void StartApi() => Factory = new ApexApiFactory();

    public HttpClient CreateClient(string? token = null)
    {
        var client = (Factory ?? throw new InvalidOperationException("The API has not been started in this scenario.")).CreateClient();
        var effective = token ?? Token;
        if (effective is not null)
        {
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", effective);
        }

        return client;
    }

    public void Dispose()
    {
        Response?.Dispose();
        Factory?.Dispose();
    }
}
