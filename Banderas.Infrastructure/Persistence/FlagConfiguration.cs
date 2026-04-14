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

        builder.Property(f => f.StrategyConfig).IsRequired().HasColumnType("jsonb");

        builder.Property(f => f.IsEnabled).IsRequired();

        builder.Property(f => f.IsArchived).IsRequired();

        builder.Property(f => f.IsSeeded).IsRequired().HasDefaultValue(false);

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
