using Banderas.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Banderas.Api.Extensions;

internal static class WebApplicationExtensions
{
    internal static async Task MigrateAsync(this WebApplication app)
    {
        using IServiceScope scope = app.Services.CreateScope();
        BanderasDbContext db = scope.ServiceProvider.GetRequiredService<BanderasDbContext>();
        await db.Database.MigrateAsync();
    }
}
