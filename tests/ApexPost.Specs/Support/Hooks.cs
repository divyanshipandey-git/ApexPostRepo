using Reqnroll;

namespace ApexPost.Specs.Support;

/// <summary>
/// Scenario hooks. Tags decide what a scenario needs: @api starts the in-memory API with a fresh SQLite database,
/// @service needs no host (its context creates its own private database). Cleanup runs after every scenario,
/// also when it failed, so no data or connection leaks into the next scenario.
/// </summary>
[Binding]
public sealed class Hooks(ApiScenarioContext api, ServiceScenarioContext service, ScenarioContext scenario)
{
    [BeforeScenario("api", Order = 10)]
    public void StartApi() => api.StartApi();

    [AfterScenario(Order = 10)]
    public void CleanUp()
    {
        api.Dispose();
        service.Dispose();
        Console.WriteLine($"Scenario \"{scenario.ScenarioInfo.Title}\" cleaned up (database and host disposed).");
    }
}
