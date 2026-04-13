using FeatureFlag.Api.Extensions;
using FeatureFlag.Api.OpenApi;
using FeatureFlag.Application;
using FeatureFlag.Infrastructure;
using FeatureFlag.Infrastructure.Seeding;
using Scalar.AspNetCore;

WebApplicationBuilder builder = WebApplication.CreateBuilder(args);

builder
    .Services.AddControllers()
    .AddJsonOptions(options =>
    {
        options.JsonSerializerOptions.Converters.Add(
            new System.Text.Json.Serialization.JsonStringEnumConverter()
        );
    });

builder.Services.AddOpenApi(options =>
{
    options.AddSchemaTransformer<EnumSchemaTransformer>();
    options.AddDocumentTransformer<ApiInfoTransformer>();
});

builder.Services.AddApplication();
builder.Services.AddInfrastructure(builder.Configuration);

WebApplication app = builder.Build();

// Must be first — wraps the entire pipeline
app.UseMiddleware<FeatureFlag.Api.Middleware.GlobalExceptionMiddleware>();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
    app.MapScalarApiReference();

    // Redirect root to Scalar UI for development convenience
    app.MapGet("/", () => Results.Redirect("/scalar/v1")).ExcludeFromDescription();
}

app.UseHttpsRedirection();
app.UseAuthorization();
app.MapControllers();

if (app.Environment.IsDevelopment())
{
    await app.MigrateAsync();

    using IServiceScope scope = app.Services.CreateScope();
    DatabaseSeeder seeder = scope.ServiceProvider.GetRequiredService<DatabaseSeeder>();
    bool reset = Environment.GetEnvironmentVariable("SEED_RESET") == "true";
    await seeder.SeedAsync(reset);
}

app.Run();

public partial class Program { }
