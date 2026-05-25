using Banderas.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Banderas.Infrastructure.Persistence;

public sealed class FlagConfiguration : IEntityTypeConfiguration<Flag>
{
    public void Configure(EntityTypeBuilder<Flag> builder)
    {
        builder.ToTable("flags");

        builder.HasKey(f => f.Id);

        builder.Property(f => f.Name).IsRequired().HasMaxLength(200);

        builder.Property(f => f.Environment).IsRequired().HasConversion<string>();

        builder.Property(f => f.StrategyType).IsRequired().HasConversion<string>();

        builder
            .Property(f => f.StrategyConfig)
            .HasField("_strategyConfig")
            .IsRequired()
            .HasColumnType("jsonb")
            .HasConversion(new StrategyConfigConverter());

        builder.Property(f => f.Description).HasMaxLength(500).IsRequired(false);

        // Tags persists as jsonb; the SQL-level default '[]' backfills existing
        // rows on migration apply and guarantees the domain invariant
        // (Flag.Tags != null). HasDefaultValueSql is required here because the
        // CLR property type is IReadOnlyList<string>, not the post-conversion string.
        builder
            .Property(f => f.Tags)
            .IsRequired()
            .HasColumnType("jsonb")
            .HasConversion(new TagListConverter())
            .HasDefaultValueSql("'[]'");

        // Variations: jsonb, NOT NULL, no permanent SQL default (DD-6, pitfall #2).
        // The default is applied via raw SQL in the migration's Up only and dropped
        // after backfill — leaving a permanent '[]' default would silently violate
        // the non-empty domain invariant on any future INSERT that forgets to set it.
        builder
            .Property(f => f.Variations)
            .HasField("_variations")
            .UsePropertyAccessMode(PropertyAccessMode.Field)
            .IsRequired()
            .HasColumnType("jsonb")
            .HasConversion(new VariationListConverter());

        builder.Property(f => f.IsEnabled).IsRequired();

        builder.Property(f => f.IsArchived).IsRequired();

        builder.Property<bool>("IsSeeded").IsRequired().HasDefaultValue(false);

        builder.Property(f => f.CreatedAt).IsRequired();

        builder.Property(f => f.UpdatedAt).IsRequired();

        builder.Property(f => f.ArchivedAt).IsRequired(false);

        // Partial unique index — only enforces uniqueness on active (non-archived) flags.
        // Without HasFilter, archiving a flag and recreating it with the same name would
        // throw a unique constraint violation because the archived row still occupies
        // the index slot. HasFilter restricts the index to rows where IsArchived = false,
        // so archived flags are invisible to the constraint.
        // This is a PostgreSQL-specific feature.
        builder
            .HasIndex(f => new { f.Name, f.Environment })
            .IsUnique()
            .HasFilter("\"IsArchived\" = false");
    }
}
