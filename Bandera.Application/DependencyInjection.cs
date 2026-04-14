using Bandera.Application.Evaluation;
using Bandera.Application.Interfaces;
using Bandera.Application.Services;
using Bandera.Application.Strategies;
using Bandera.Application.Validators;
using Bandera.Domain.Interfaces;
using FluentValidation;
using Microsoft.Extensions.DependencyInjection;

namespace Bandera.Application;

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
        services.AddScoped<IBanderaService, BanderaService>();

        return services;
    }
}
