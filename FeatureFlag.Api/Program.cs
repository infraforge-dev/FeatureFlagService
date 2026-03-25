using FeatureFlag.Application;
using FeatureFlag.Infrastructure;

var builder = WebApplication.CreateBuilder(args);

builder
    .Services.AddControllers()
    .AddJsonOptions(options =>
    {
        // Serialize enums as strings in JSON responses ("Production" not 3).
        // Matches how EF Core stores enums in the database — consistent
        // string representation throughout the stack.
        options.JsonSerializerOptions.Converters.Add(
            new System.Text.Json.Serialization.JsonStringEnumConverter()
        );
    });

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddOpenApi();

builder.Services.AddApplication();
builder.Services.AddInfrastructure(builder.Configuration);

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();

    // Redirect root to OpenAPI docs for convenience during development
    app.MapGet("/", () => Results.Redirect("/openapi/v1.json")).ExcludeFromDescription();
}

app.UseHttpsRedirection();
app.UseAuthorization();
app.MapControllers();

app.Run();
