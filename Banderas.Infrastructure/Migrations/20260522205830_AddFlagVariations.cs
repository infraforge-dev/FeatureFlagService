using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Banderas.Infrastructure.Migrations
{
    /// <inheritdoc />
    /// <remarks>
    /// Adds the <c>Variations</c> column and backfills existing rows with the
    /// default two-variation boolean menu <c>[{off,false},{on,true}]</c>. The SQL
    /// default <c>'[]'</c> exists only during the transient window between
    /// <c>ADD COLUMN</c> and the backfill <c>UPDATE</c> — it is dropped before the
    /// migration completes so any future INSERT without <c>Variations</c> fails
    /// rather than silently violating the domain's non-empty invariant (DD-6).
    ///
    /// Sequence inside <c>Up</c>:
    ///   1. <c>ADD COLUMN ... DEFAULT '[]'</c> — metadata-only on PG 11+.
    ///   2. <c>UPDATE flags SET variations = '[...]'</c> — backfills existing rows.
    ///   3. <c>ALTER COLUMN ... DROP DEFAULT</c> — metadata-only; removes the safety net.
    /// </remarks>
    public partial class AddFlagVariations : Migration
    {
        private const string DefaultVariationMenuJson =
            "[{\"key\":\"off\",\"kind\":\"Boolean\",\"value\":\"false\"},"
            + "{\"key\":\"on\",\"kind\":\"Boolean\",\"value\":\"true\"}]";

        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Step 1: add the column with a transient default so the NOT NULL
            // constraint can be satisfied during the brief window before backfill.
            migrationBuilder.AddColumn<string>(
                name: "Variations",
                table: "flags",
                type: "jsonb",
                nullable: false,
                defaultValueSql: "'[]'"
            );

            // Step 2: backfill every existing row with the default boolean menu.
            // Single SQL statement; instant on dev (six flags), sub-second on a
            // hypothetical production DB with thousands of flags (DD-6).
            migrationBuilder.Sql(
                $"UPDATE flags SET \"Variations\" = '{DefaultVariationMenuJson}'::jsonb;"
            );

            // Step 3: drop the SQL default. Leaving '[]' permanently would let any
            // future INSERT that forgets Variations sneak past NOT NULL while
            // silently violating the non-empty domain invariant (DD-6 pitfall #2).
            migrationBuilder.Sql(
                "ALTER TABLE flags ALTER COLUMN \"Variations\" DROP DEFAULT;"
            );
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(name: "Variations", table: "flags");
        }
    }
}
