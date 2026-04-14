using Banderas.Domain.Interfaces;
using Banderas.Infrastructure.Persistence;
using Banderas.Infrastructure.Seeding;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Banderas.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration
    )
    {
        services.AddDbContext<BanderasDbContext>(options =>
            options.UseNpgsql(configuration.GetConnectionString("DefaultConnection"))
        );

        services.AddScoped<IBanderasRepository, BanderasRepository>();
        services.AddScoped<DatabaseSeeder>();

        return services;
    }
}
