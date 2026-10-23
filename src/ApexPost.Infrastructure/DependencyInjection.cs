
using ApexPost.Application.Abstractions.Repositories;
using ApexPost.Infrastructure.Persistence;
using ApexPost.Infrastructure.Persistence.Repositories;
using Microsoft.Extensions.DependencyInjection;

namespace ApexPost.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(
        this IServiceCollection services,
        Action<JsonPersistenceOptions>? configure = null)
    {
        ArgumentNullException.ThrowIfNull(services);

        var options = new JsonPersistenceOptions();
        configure?.Invoke(options);

        ArgumentException.ThrowIfNullOrWhiteSpace(options.DataDirectory);
        options.DataDirectory = Path.GetFullPath(options.DataDirectory);

        services.AddSingleton(options);
        services.AddSingleton<JsonAuditLog>();

        services.AddSingleton<IInventoryRepository, JsonInventoryRepository>();
        services.AddSingleton<ICounterTransactionRepository, JsonCounterTransactionRepository>();
        services.AddSingleton<IShipmentRepository, JsonShipmentRepository>();
        services.AddSingleton<IMoneyOrderRepository, JsonMoneyOrderRepository>();

        return services;
    }
}
