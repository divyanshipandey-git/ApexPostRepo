using ApexPost.Application.Services.Branches;
using ApexPost.Application.Services.Inventory;
using ApexPost.Application.Services.Products;
using ApexPost.Application.Services.Staff;
using ApexPost.Application.Services.Shipping;
using ApexPost.Application.Services.Transactions;
using ApexPost.Application.Abstractions.Repositories;
using ApexPost.Domain.Entities;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace ApexPost.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.TryAddSingleton<TimeProvider>(TimeProvider.System);
        services.TryAddScoped<BranchService>();
        services.TryAddScoped<ProductCatalogService>();
        services.TryAddScoped<InventoryService>();
        services.TryAddScoped<InventoryInquiryService>();
        services.TryAddScoped<StaffAuthenticationService>();
        services.TryAddScoped<CourierStatusService>();
        services.TryAddScoped<ShippingService>(provider => new ShippingService(
            provider.GetRequiredService<IShipmentRepository>(),
            Array.Empty<ShippingPricingRule>()));
        services.TryAddScoped<CounterSaleService>();
        services.TryAddScoped<TransactionVoidService>();

        return services;
    }
}
