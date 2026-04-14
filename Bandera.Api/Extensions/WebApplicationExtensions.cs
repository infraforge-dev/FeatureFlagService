using Bandera.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Bandera.Api.Extensions;

internal static class WebApplicationExtensions
{
    internal static async Task MigrateAsync(this WebApplication app)
    {
        using IServiceScope scope = app.Services.CreateScope();
        BanderaDbContext db = scope.ServiceProvider.GetRequiredService<BanderaDbContext>();
        await db.Database.MigrateAsync();
    }
}
