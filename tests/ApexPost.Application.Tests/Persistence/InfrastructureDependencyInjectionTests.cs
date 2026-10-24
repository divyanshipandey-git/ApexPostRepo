
using ApexPost.Application.Abstractions.Repositories;
using ApexPost.Application.Abstractions.Persistence;
using ApexPost.Infrastructure;
using ApexPost.Infrastructure.Persistence;
using ApexPost.Infrastructure.Persistence.Repositories;
using Microsoft.Extensions.Configuration;
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
        Assert.IsType<JsonCounterRepository>(provider.GetRequiredService<ICounterRepository>());
        Assert.NotNull(provider.GetRequiredService<ICounterTransactionRepository>());
        Assert.NotNull(provider.GetRequiredService<IShipmentRepository>());
        Assert.NotNull(provider.GetRequiredService<IMoneyOrderRepository>());
        Assert.IsType<JsonBranchRepository>(provider.GetRequiredService<IBranchRepository>());
        Assert.IsType<JsonCounterRepository>(provider.GetRequiredService<ICounterRepository>());
        Assert.IsType<JsonProductRepository>(provider.GetRequiredService<IProductRepository>());
        Assert.IsType<JsonStaffRepository>(provider.GetRequiredService<IStaffRepository>());
        Assert.NotNull(provider.GetRequiredService<JsonAuditLog>());
        Assert.Same(
            provider.GetRequiredService<JsonAuditLog>(),
            provider.GetRequiredService<IAuditStore>());
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

    [Fact]
    public void AddInfrastructure_BindsDataDirectoryFromConfiguration()
    {
        using var directory = new JsonRepositoryTestDirectory();
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["JsonPersistence:DataDirectory"] = directory.Path
            })
            .Build();
        var services = new ServiceCollection();

        services.AddInfrastructure(configuration);

        using var provider = services.BuildServiceProvider();
        var options = provider.GetRequiredService<JsonPersistenceOptions>();

        Assert.Equal(Path.GetFullPath(directory.Path), options.DataDirectory);
    }
}
