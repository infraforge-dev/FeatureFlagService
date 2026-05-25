using Banderas.Domain.Entities;
using Banderas.Domain.Enums;
using Banderas.Domain.ValueObjects;

namespace Banderas.Application.DTOs;

public static class FlagMappings
{
    public static FlagResponse ToResponse(this Flag flag) =>
        new(
            flag.Id,
            flag.Name,
            flag.Environment,
            flag.IsEnabled,
            flag.IsArchived,
            flag.StrategyType,
            flag.StrategyConfig.RawJson,
            flag.CreatedAt,
            flag.UpdatedAt
        )
        {
            Description = flag.Description,
            Tags = flag.Tags,
            Variations = flag.Variations.Select(ToResponse).ToList(),
        };

    /// <summary>
    /// Maps a single domain <see cref="Variation"/> to its wire-form response.
    /// </summary>
    public static VariationResponse ToResponse(this Variation variation) =>
        new(variation.Key, variation.Kind.ToString(), variation.Value);

    /// <summary>
    /// Maps a wire-form <see cref="VariationRequest"/> to a domain <see cref="Variation"/>.
    /// The validator runs first and catches unknown kinds with a field-level 400; this
    /// helper assumes the request has already been validated.
    /// </summary>
    public static Variation ToDomain(this VariationRequest request)
    {
        if (!TryParseKind(request.Kind, out VariationKind kind))
        {
            throw new ArgumentException(
                $"Unknown VariationKind '{request.Kind}'. Validator should have caught this.",
                nameof(request)
            );
        }

        return new Variation(request.Key, kind, request.Value);
    }

    /// <summary>
    /// Case-insensitive parse of a kind name. Used by both the validator (to check
    /// kind validity) and the mapping helper.
    /// </summary>
    public static bool TryParseKind(string? kind, out VariationKind result) =>
        Enum.TryParse(kind, ignoreCase: true, out result) && Enum.IsDefined(result);
}
