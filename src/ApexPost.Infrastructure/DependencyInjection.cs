
using ApexPost.Application.Abstractions.Repositories;
using ApexPost.Application.Abstractions.Persistence;
using ApexPost.Infrastructure.Persistence;
using ApexPost.Infrastructure.Persistence.Repositories;
using ApexPost.Infrastructure.Security;
using ApexPost.Application.Abstractions.Security;
using ApexPost.Application.Abstractions.Reports;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace ApexPost.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(configuration);

        return AddInfrastructure(
            services,
            options => options.DataDirectory =
                configuration["JsonPersistence:DataDirectory"] ?? options.DataDirectory);
    }

    public static IServiceCollection AddInfrastructure(
        this IServiceCollection services,
        Action<JsonPersistenceOptions>? configure = null)
    {
        ArgumentNullException.ThrowIfNull(services);

        var options = new JsonPersistenceOptions();
        configure?.Invoke(options);

        ArgumentException.ThrowIfNullOrWhiteSpace(options.DataDirectory);
        options.DataDirectory = JsonPersistenceOptions.ResolveDataDirectory(options.DataDirectory);

        services.TryAddSingleton(options);
        services.TryAddSingleton<JsonAuditLog>();
        services.TryAddSingleton<IAuditStore>(provider => provider.GetRequiredService<JsonAuditLog>());

        services.TryAddSingleton<IBranchRepository>(provider =>
            new JsonBranchRepository(
                provider.GetRequiredService<JsonPersistenceOptions>().DataDirectory));
        services.TryAddSingleton<IProductRepository>(provider =>
            new JsonProductRepository(
                provider.GetRequiredService<JsonPersistenceOptions>().DataDirectory));
        services.TryAddSingleton<IStaffRepository>(provider =>
            new JsonStaffRepository(
                provider.GetRequiredService<JsonPersistenceOptions>().DataDirectory));
        services.TryAddSingleton<ICounterRepository>(provider =>
            new JsonCounterRepository(provider.GetRequiredService<JsonPersistenceOptions>()));
        services.TryAddSingleton<IStaffCredentialRepository>(provider =>
            new JsonStaffCredentialRepository(
                provider.GetRequiredService<JsonPersistenceOptions>().DataDirectory));
        services.TryAddSingleton<IStaffPasswordHasher, Pbkdf2StaffPasswordHasher>();

        services.TryAddSingleton<IInventoryRepository, JsonInventoryRepository>();
        services.TryAddSingleton<ICounterTransactionRepository, JsonCounterTransactionRepository>();
        services.TryAddSingleton<IShipmentRepository, JsonShipmentRepository>();
        services.TryAddSingleton<IMoneyOrderRepository, JsonMoneyOrderRepository>();
        services.TryAddScoped<IReportDataSource, JsonReportDataSource>();

        return services;
    }
}
