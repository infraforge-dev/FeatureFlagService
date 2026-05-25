using System.Text.Json;
using Banderas.Domain.Enums;
using Banderas.Domain.ValueObjects;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace Banderas.Infrastructure.Persistence;

/// <summary>
/// Converts <see cref="IReadOnlyList{T}"/> of <see cref="Variation"/> to/from
/// the <c>jsonb</c> column. Mirrors <see cref="TagListConverter"/>'s shape but
/// reconstructs typed VOs on read.
/// </summary>
/// <remarks>
/// The migration backfill guarantees that no row contains NULL after apply;
/// the null-fallback to an empty list is a defensive safety net only — a
/// genuine NULL would bubble through to the aggregate which would then throw
/// <c>FlagDomainException</c> on the non-empty invariant. The fallback exists
/// so a corrupted row produces a more diagnosable failure than a NullReferenceException
/// at deserialization time.
/// </remarks>
public sealed class VariationListConverter : ValueConverter<IReadOnlyList<Variation>, string>
{
    private static readonly JsonSerializerOptions WriteOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        Converters = { new System.Text.Json.Serialization.JsonStringEnumConverter() },
    };

    private static readonly JsonSerializerOptions ReadOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        Converters = { new System.Text.Json.Serialization.JsonStringEnumConverter() },
    };

    public VariationListConverter()
        : base(
            variations =>
                JsonSerializer.Serialize(
                    variations.Select(v => new VariationDto(v.Key, v.Kind, v.Value)),
                    WriteOptions
                ),
            json => Deserialize(json)
        ) { }

    private static List<Variation> Deserialize(string json)
    {
        if (string.IsNullOrWhiteSpace(json) || json == "null")
        {
            return [];
        }

        List<VariationDto>? dtos = JsonSerializer.Deserialize<List<VariationDto>>(
            json,
            ReadOptions
        );
        if (dtos is null)
        {
            return [];
        }

        // Re-running the VO ctor on read re-validates the stored shape. Migration
        // backfill guarantees this passes; a hand-corrupted row will throw here,
        // which is the desired loud failure.
        return dtos.Select(d => new Variation(d.Key, d.Kind, d.Value)).ToList();
    }

    /// <summary>
    /// JSON-shape DTO used purely for converter (de)serialization. Mirrors the
    /// wire contract: { key, kind, value }, camelCase, kind-as-string.
    /// </summary>
    private sealed record VariationDto(string Key, VariationKind Kind, string Value);
}
