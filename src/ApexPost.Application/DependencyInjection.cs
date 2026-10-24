using ApexPost.Application.Services.Branches;
using ApexPost.Application.Services.Inventory;
using ApexPost.Application.Services.Products;
using ApexPost.Application.Services.Staff;
using ApexPost.Application.Services.Shipping;
using ApexPost.Application.Services.Transactions;
using ApexPost.Application.Services.MoneyOrders;
using ApexPost.Application.Services.Notifications;
using ApexPost.Domain.ValueObjects;
using ApexPost.Application.Abstractions.Repositories;
using ApexPost.Application.Abstractions.Notifications;
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
        var moneyOrderSettings = new MoneyOrderSettings(new Money(10m), new Money(10_000m));
        services.TryAddSingleton(moneyOrderSettings);
        services.TryAddSingleton(new MoneyOrderPolicy(moneyOrderSettings.VerificationThreshold));
        services.TryAddScoped<BranchService>();
        services.TryAddScoped<ProductCatalogService>();
        services.TryAddScoped<InventoryService>();
        services.TryAddScoped<InventoryInquiryService>();
        services.TryAddScoped<StaffAuthenticationService>();
        services.TryAddScoped<MoneyOrderService>();
        services.TryAddScoped<CourierStatusService>();
        services.TryAddScoped<ShippingService>(provider => new ShippingService(
            provider.GetRequiredService<IShipmentRepository>(),
            Array.Empty<ShippingPricingRule>()));
        services.TryAddScoped<CounterSaleService>();
        services.TryAddScoped<TransactionVoidService>();
        services.TryAddScoped<TransactionRefundService>();
        services.TryAddSingleton<SimulatedNotificationService>();
        services.TryAddSingleton<INotificationService>(provider =>
            provider.GetRequiredService<SimulatedNotificationService>());

        return services;
    }
}
