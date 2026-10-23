using Microsoft.Extensions.DependencyInjection;

namespace ApexPost.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        // Register application use cases here as they are implemented.
        return services;
    }
}
