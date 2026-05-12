using Banderas.Domain.Entities;
using Banderas.Domain.Enums;
using Banderas.Domain.ValueObjects;
using Banderas.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.Extensions.Logging;

namespace Banderas.Infrastructure.Seeding;

public sealed class DatabaseSeeder(BanderasDbContext db, ILogger<DatabaseSeeder> logger)
{
    private static readonly SeedRecord[] SeedManifest =
    [
        new(
            "dark-mode",
            EnvironmentType.Development,
            true,
            RolloutStrategy.None,
            "{}",
            "Toggles the dark theme across the web app.",
            ["squad-ui", "theme"]
        ),
        new(
            "new-dashboard",
            EnvironmentType.Development,
            true,
            RolloutStrategy.Percentage,
            """{"percentage":30}""",
            "Gradual rollout of the redesigned analytics dashboard.",
            ["squad-analytics", "release-q2"]
        ),
        new(
            "beta-features",
            EnvironmentType.Development,
            true,
            RolloutStrategy.RoleBased,
            """{"roles":["Admin","Beta"]}""",
            "Opt-in surface for admins and beta testers to preview unreleased features.",
            ["beta-program", "internal"]
        ),
        new(
            "maintenance-mode",
            EnvironmentType.Development,
            false,
            RolloutStrategy.None,
            "{}",
            "Kill switch that disables write paths during planned maintenance windows.",
            ["ops", "killswitch"]
        ),
        new(
            "dark-mode",
            EnvironmentType.Staging,
            true,
            RolloutStrategy.None,
            "{}",
            "Toggles the dark theme across the web app.",
            ["squad-ui", "theme"]
        ),
        new(
            "new-dashboard",
            EnvironmentType.Staging,
            true,
            RolloutStrategy.Percentage,
            """{"percentage":50}""",
            "Widened staging rollout of the redesigned analytics dashboard.",
            ["squad-analytics", "release-q2", "staging-canary"]
        ),
    ];

    public async Task SeedAsync(bool reset, CancellationToken ct = default)
    {
        if (reset)
        {
            await ResetSeedAsync(ct);
            return;
        }

        await SeedMissingAsync(ct);
    }

    private async Task SeedMissingAsync(CancellationToken ct)
    {
        int insertedCount = 0;

        foreach (SeedRecord record in SeedManifest)
        {
            bool slotOccupied = await db.Flags.AnyAsync(
                f => f.Name == record.Name && f.Environment == record.Environment && !f.IsArchived,
                ct
            );

            if (slotOccupied)
            {
                if (logger.IsEnabled(LogLevel.Debug))
                {
                    logger.LogDebug(
                        "Seed slot '{Name}' ({Environment}) is occupied - skipping.",
                        record.Name,
                        record.Environment
                    );
                }

                continue;
            }

            EntityEntry<Flag> entry = await db.Flags.AddAsync(record.ToFlag(), ct);
            entry.Property("IsSeeded").CurrentValue = true;
            insertedCount++;
        }

        if (insertedCount == 0)
        {
            logger.LogInformation("Seeding skipped - all seed slots are occupied.");
            return;
        }

        await db.SaveChangesAsync(ct);
        if (logger.IsEnabled(LogLevel.Information))
        {
            logger.LogInformation("Seeded {Count} flag(s).", insertedCount);
        }
    }

    private async Task ResetSeedAsync(CancellationToken ct)
    {
        logger.LogWarning("SEED_RESET=true - deleting all seeded records before re-seeding.");

        await db.Flags.Where(f => EF.Property<bool>(f, "IsSeeded")).ExecuteDeleteAsync(ct);

        int insertedCount = 0;

        foreach (SeedRecord record in SeedManifest)
        {
            bool manualSlotOccupied = await db.Flags.AnyAsync(
                f =>
                    f.Name == record.Name
                    && f.Environment == record.Environment
                    && !f.IsArchived
                    && !EF.Property<bool>(f, "IsSeeded"),
                ct
            );

            if (manualSlotOccupied)
            {
                logger.LogWarning(
                    "Seed slot '{Name}' ({Environment}) is occupied by a manual flag - skipping. Delete the manual flag and re-run SEED_RESET=true to restore this baseline slot.",
                    record.Name,
                    record.Environment
                );
                continue;
            }

            EntityEntry<Flag> entry = await db.Flags.AddAsync(record.ToFlag(), ct);
            entry.Property("IsSeeded").CurrentValue = true;
            insertedCount++;
        }

        await db.SaveChangesAsync(ct);
        if (logger.IsEnabled(LogLevel.Information))
        {
            logger.LogInformation("Re-seeded {Count} flag(s).", insertedCount);
        }
    }

    private sealed record SeedRecord(
        string Name,
        EnvironmentType Environment,
        bool IsEnabled,
        RolloutStrategy StrategyType,
        string StrategyConfig,
        string? Description = null,
        IReadOnlyList<string>? Tags = null
    )
    {
        public Flag ToFlag()
        {
            var config = new StrategyConfig(StrategyType, StrategyConfig);
            return new Flag(Name, Environment, IsEnabled, StrategyType, config, Description, Tags);
        }
    }
}
