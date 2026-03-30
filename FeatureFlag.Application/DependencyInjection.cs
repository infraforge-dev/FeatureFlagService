using FeatureFlag.Application.Evaluation;
using FeatureFlag.Application.Interfaces;
using FeatureFlag.Application.Services;
using FeatureFlag.Application.Strategies;
using FeatureFlag.Application.Validators;
using FeatureFlag.Domain.Interfaces;
using FluentValidation;
using Microsoft.Extensions.DependencyInjection;

namespace FeatureFlag.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        // Validators — registered explicitly; IValidator<T> injected into controllers
        services.AddScoped<IValidator<DTOs.CreateFlagRequest>, CreateFlagRequestValidator>();
        services.AddScoped<IValidator<DTOs.UpdateFlagRequest>, UpdateFlagRequestValidator>();
        services.AddScoped<IValidator<DTOs.EvaluationRequest>, EvaluationRequestValidator>();

        // Strategies — Singleton: stateless, safe to share across requests
        services.AddSingleton<IRolloutStrategy, NoneStrategy>();
        services.AddSingleton<IRolloutStrategy, PercentageStrategy>();
        services.AddSingleton<IRolloutStrategy, RoleStrategy>();

        // Evaluator — Singleton: depends only on Singleton strategies
        services.AddSingleton<FeatureEvaluator>();

        // Service — Scoped: depends on Scoped repository
        services.AddScoped<IFeatureFlagService, FeatureFlagService>();

        return services;
    }
}
