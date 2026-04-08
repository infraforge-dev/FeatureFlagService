using FeatureFlag.Api.OpenApi;
using FeatureFlag.Application;
using FeatureFlag.Infrastructure;
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

app.Run();

public partial class Program { }
