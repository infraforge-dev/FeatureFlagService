using Azure.Identity;
using Banderas.Application.AI;
using Banderas.Application.Telemetry;
using Banderas.Domain.Interfaces;
using Banderas.Infrastructure.AI;
using Banderas.Infrastructure.Persistence;
using Banderas.Infrastructure.Seeding;
using Banderas.Infrastructure.Telemetry;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.SemanticKernel;

namespace Banderas.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration,
        IHostEnvironment environment
    )
    {
        services.AddDbContext<BanderasDbContext>(options =>
            options.UseNpgsql(configuration.GetConnectionString("DefaultConnection"))
        );

        services.AddScoped<IBanderasRepository, BanderasRepository>();
        services.AddScoped<DatabaseSeeder>();

        if (environment.IsEnvironment("Testing"))
        {
            services.AddSingleton<ITelemetryService, NullTelemetryService>();
        }
        else
        {
            services.AddSingleton<ITelemetryService, ApplicationInsightsTelemetryService>();

            string endpoint =
                configuration["AzureOpenAI:Endpoint"]
                ?? throw new InvalidOperationException("AzureOpenAI:Endpoint is required.");

            string deploymentName = configuration["AzureOpenAI:DeploymentName"] ?? "gpt-5-mini";

            IKernelBuilder kernelBuilder = Kernel.CreateBuilder();
            kernelBuilder.AddAzureOpenAIChatCompletion(
                deploymentName,
                endpoint,
                new DefaultAzureCredential()
            );

            services.AddSingleton(kernelBuilder.Build());
            services.AddScoped<IAiFlagAnalyzer, AiFlagAnalyzer>();
        }

        return services;
    }
}
