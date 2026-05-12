using System.Text.Json;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace Banderas.Infrastructure.Persistence;

/// <summary>
/// Converts <see cref="IReadOnlyList{T}"/> of tag strings to/from the <c>jsonb</c> column.
/// Mirrors the <see cref="StrategyConfigConverter"/> shape but for a plain
/// <c>List&lt;string&gt;</c> rather than a typed Value Object. Null payloads on read
/// fall back to an empty list so the domain invariant (<c>Flag.Tags != null</c>) holds.
/// </summary>
public sealed class TagListConverter : ValueConverter<IReadOnlyList<string>, string>
{
    private static readonly JsonSerializerOptions Options = new();

    public TagListConverter()
        : base(
            tags => JsonSerializer.Serialize(tags, Options),
            json => JsonSerializer.Deserialize<List<string>>(json, Options) ?? new List<string>()
        ) { }
}
