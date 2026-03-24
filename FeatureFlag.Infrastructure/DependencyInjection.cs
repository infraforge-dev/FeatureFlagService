using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace FeatureFlag.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        // TODO: services.AddDbContext<FeatureFlagDbContext>(...)
        // TODO: services.AddScoped<IFeatureFlagRepository, FeatureFlagRepository>()

        return services;
    }
}
