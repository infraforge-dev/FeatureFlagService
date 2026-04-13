using FeatureFlag.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace FeatureFlag.Api.Extensions;

internal static class WebApplicationExtensions
{
    internal static async Task MigrateAsync(this WebApplication app)
    {
        using IServiceScope scope = app.Services.CreateScope();
        FeatureFlagDbContext db = scope.ServiceProvider.GetRequiredService<FeatureFlagDbContext>();
        await db.Database.MigrateAsync();
    }
}
