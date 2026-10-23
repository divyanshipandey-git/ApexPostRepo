
using ApexPost.Application.Abstractions.Repositories;
using ApexPost.Infrastructure;
using ApexPost.Infrastructure.Persistence;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace ApexPost.Application.Tests.Persistence;

public sealed class InfrastructureDependencyInjectionTests
{
    [Fact]
    public void AddInfrastructure_RegistersAllJsonRepositories()
    {
        using var directory = new JsonRepositoryTestDirectory();
        var services = new ServiceCollection();

        services.AddInfrastructure(options =>
        {
            options.DataDirectory = directory.Path;
        });

        using var provider = services.BuildServiceProvider();

        Assert.NotNull(provider.GetRequiredService<IInventoryRepository>());
        Assert.NotNull(provider.GetRequiredService<ICounterTransactionRepository>());
        Assert.NotNull(provider.GetRequiredService<IShipmentRepository>());
        Assert.NotNull(provider.GetRequiredService<IMoneyOrderRepository>());
        Assert.NotNull(provider.GetRequiredService<JsonAuditLog>());
    }

    [Fact]
    public void AddInfrastructure_UsesConfiguredDataDirectory()
    {
        using var directory = new JsonRepositoryTestDirectory();
        var services = new ServiceCollection();

        services.AddInfrastructure(options =>
        {
            options.DataDirectory = directory.Path;
        });

        using var provider = services.BuildServiceProvider();

        var options = provider.GetRequiredService<JsonPersistenceOptions>();

        Assert.Equal(
            Path.GetFullPath(directory.Path),
            options.DataDirectory);
    }
}
