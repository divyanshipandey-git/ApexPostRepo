using Microsoft.Extensions.DependencyInjection;

namespace ApexPost.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services)
    {
        // Register JSON repositories, audit store and simulated integrations here.
        return services;
    }
}
