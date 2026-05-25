using Banderas.Infrastructure.Persistence;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using Testcontainers.PostgreSql;

namespace Banderas.Tests.Integration;

/// <summary>
/// AC-10 — covers the AddFlagVariations migration's backfill behavior end-to-end.
/// Spins up a dedicated Postgres container, migrates up to the migration *before*
/// AddFlagVariations, inserts a flag row with hand-rolled SQL (because the
/// pre-variations <c>Flag</c> aggregate no longer exists), then applies
/// AddFlagVariations and verifies:
///   • the row's Variations column was backfilled to the default menu,
///   • the column is NOT NULL,
///   • no SQL-level default remains on the column,
///   • Down() cleanly drops the column.
/// </summary>
[Trait("Category", "Integration")]
public sealed class MigrationBackfillTests : IAsyncLifetime
{
    private readonly PostgreSqlContainer _postgres = new PostgreSqlBuilder("postgres:16").Build();

    public Task InitializeAsync() => _postgres.StartAsync();

    public Task DisposeAsync() => _postgres.DisposeAsync().AsTask();

    private const string AddFlagVariationsMigrationId = "20260522205830_AddFlagVariations";
    private const string PriorMigrationId = "20260512194041_AddFlagDescriptionAndTags";

    private BanderasDbContext CreateContext()
    {
        DbContextOptions<BanderasDbContext> options =
            new DbContextOptionsBuilder<BanderasDbContext>()
                .UseNpgsql(_postgres.GetConnectionString())
                .Options;
        return new BanderasDbContext(options);
    }

    [Fact]
    [Trait("Category", "Integration")]
    public async Task AddFlagVariations_BackfillsExistingRows_AndDropsDefaultAsync()
    {
        // Migrate up to the *previous* migration so we can simulate a pre-existing
        // legacy row that predates the Variations column.
        using (BanderasDbContext beforeCtx = CreateContext())
        {
            IMigrator migrator = beforeCtx.GetService<IMigrator>();
            await migrator.MigrateAsync(PriorMigrationId);
        }

        // Insert a pre-existing flag row via raw SQL. We can't use the aggregate
        // because the current Flag constructor already requires Variations.
        // Note: avoid Database.ExecuteSqlRawAsync because of format-placeholder
        // interpretation of '{}'::jsonb. Use Npgsql directly.
        Guid legacyId = Guid.NewGuid();
        await using (NpgsqlConnection insertConn = new(_postgres.GetConnectionString()))
        {
            await insertConn.OpenAsync();
            await using NpgsqlCommand insertCmd = new(
                """
                INSERT INTO flags
                    ("Id", "Name", "Environment", "IsEnabled", "IsArchived",
                     "StrategyType", "StrategyConfig", "Description", "Tags",
                     "IsSeeded", "CreatedAt", "UpdatedAt")
                VALUES
                    (@id, 'legacy-flag', 'Development', true, false,
                     'None', '{}'::jsonb, NULL, '[]'::jsonb,
                     false, NOW() AT TIME ZONE 'UTC', NOW() AT TIME ZONE 'UTC')
                """,
                insertConn
            );
            insertCmd.Parameters.AddWithValue("id", legacyId);
            await insertCmd.ExecuteNonQueryAsync();
        }

        // Apply AddFlagVariations.
        using (BanderasDbContext upCtx = CreateContext())
        {
            IMigrator migrator = upCtx.GetService<IMigrator>();
            await migrator.MigrateAsync(AddFlagVariationsMigrationId);
        }

        // Assert: backfill ran.
        using (BanderasDbContext assertCtx = CreateContext())
        {
            await using NpgsqlConnection conn = new(_postgres.GetConnectionString());
            await conn.OpenAsync();

            await using (
                NpgsqlCommand cmd = new(
                    "SELECT \"Variations\"::text FROM flags WHERE \"Id\" = @id",
                    conn
                )
            )
            {
                cmd.Parameters.AddWithValue("id", legacyId);
                object? value = await cmd.ExecuteScalarAsync();
                value.Should().NotBeNull();
                string json = value!.ToString()!;
                json.Should().Contain("\"key\": \"off\"").And.Contain("\"value\": \"false\"");
                json.Should().Contain("\"key\": \"on\"").And.Contain("\"value\": \"true\"");
            }

            // Assert: column is NOT NULL.
            await using (
                NpgsqlCommand cmd = new(
                    """
                    SELECT is_nullable FROM information_schema.columns
                    WHERE table_name = 'flags' AND column_name = 'Variations'
                    """,
                    conn
                )
            )
            {
                object? nullable = await cmd.ExecuteScalarAsync();
                nullable.Should().Be("NO");
            }

            // Assert: no permanent SQL default remains.
            await using (
                NpgsqlCommand cmd = new(
                    """
                    SELECT column_default FROM information_schema.columns
                    WHERE table_name = 'flags' AND column_name = 'Variations'
                    """,
                    conn
                )
            )
            {
                object? def = await cmd.ExecuteScalarAsync();
                (def == null || def is DBNull)
                    .Should()
                    .BeTrue("SQL-level default must be dropped after backfill (DD-6).");
            }
        }
    }

    [Fact]
    [Trait("Category", "Integration")]
    public async Task AddFlagVariations_Down_DropsVariationsColumnCleanlyAsync()
    {
        // Migrate fully up.
        using (BanderasDbContext upCtx = CreateContext())
        {
            await upCtx.Database.MigrateAsync();
        }

        // Roll back to the previous migration.
        using (BanderasDbContext downCtx = CreateContext())
        {
            IMigrator migrator = downCtx.GetService<IMigrator>();
            await migrator.MigrateAsync(PriorMigrationId);
        }

        await using NpgsqlConnection conn = new(_postgres.GetConnectionString());
        await conn.OpenAsync();
        await using NpgsqlCommand cmd = new(
            """
            SELECT column_name FROM information_schema.columns
            WHERE table_name = 'flags' AND column_name = 'Variations'
            """,
            conn
        );
        object? result = await cmd.ExecuteScalarAsync();
        result.Should().BeNull("Down() must drop the Variations column cleanly.");
    }
}
