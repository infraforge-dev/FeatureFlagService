using Banderas.Domain.Enums;
using Banderas.Domain.Exceptions;
using Banderas.Domain.ValueObjects;

namespace Banderas.Domain.Entities;

public class Flag
{
    public Guid Id { get; init; } = Guid.NewGuid();
    public uint Version { get; private set; }
    public string Name { get; private set; }
    public EnvironmentType Environment { get; private set; }
    public bool IsEnabled { get; private set; }
    public bool IsArchived { get; private set; }
    public RolloutStrategy StrategyType { get; private set; }
    public string? Description { get; private set; }
    public IReadOnlyList<string> Tags { get; private set; } = [];

    public IReadOnlyList<Variation> Variations
    {
        get => field;
        private set => field = value;
    } = [];

    public StrategyConfig StrategyConfig
    {
        get
        {
            if (field.ValidatedFor != StrategyType)
            {
                field = new StrategyConfig(StrategyType, field.RawJson);
            }
            return field;
        }
        private set => field = value;
    } = null!;

    public DateTime CreatedAt { get; init; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; private set; } = DateTime.UtcNow;
    public DateTime? ArchivedAt { get; private set; }

    public Flag(
        string name,
        EnvironmentType environment,
        bool isEnabled,
        RolloutStrategy strategyType,
        StrategyConfig strategyConfig,
        IReadOnlyList<Variation> variations,
        string? description = null,
        IReadOnlyList<string>? tags = null
    )
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentNullException.ThrowIfNull(variations);

        if (strategyConfig.ValidatedFor != strategyType)
        {
            throw new FlagDomainException(
                $"StrategyConfig was validated for '{strategyConfig.ValidatedFor}' but Flag strategy is '{strategyType}'."
            );
        }

        EnsureVariationMenuIsValid(variations);

        Name = name;
        Environment = environment;
        IsEnabled = isEnabled;
        StrategyType = strategyType;
        StrategyConfig = strategyConfig;
        Description = description;
        Tags = tags ?? [];
        Variations = variations;
    }

    private Flag() // EF Core target
    {
        Name = string.Empty;
        StrategyConfig = new StrategyConfig(RolloutStrategy.None, "{}");
    }

    public void UpdateName(string name)
    {
        EnsureNotArchived();
        ArgumentException.ThrowIfNullOrWhiteSpace(name);

        Name = name;
        TrackUpdate();
    }

    public void Reconfigure(
        bool isEnabled,
        RolloutStrategy strategyType,
        StrategyConfig strategyConfig
    )
    {
        EnsureNotArchived();

        if (strategyConfig.ValidatedFor != strategyType)
        {
            throw new FlagDomainException(
                $"StrategyConfig was validated for '{strategyConfig.ValidatedFor}' but Flag strategy is '{strategyType}'."
            );
        }

        IsEnabled = isEnabled;
        StrategyType = strategyType;
        StrategyConfig = strategyConfig;
        TrackUpdate();
    }

    public void Archive()
    {
        EnsureNotArchived();

        IsArchived = true;
        ArchivedAt = DateTime.UtcNow;
        TrackUpdate();
    }

    public void UpdateMetadata(string? description, IReadOnlyList<string> tags)
    {
        EnsureNotArchived();
        ArgumentNullException.ThrowIfNull(tags);

        Description = description;
        Tags = tags;
        TrackUpdate();
    }

    public void UpdateVariations(IReadOnlyList<Variation> variations)
    {
        EnsureNotArchived();
        ArgumentNullException.ThrowIfNull(variations);
        EnsureVariationMenuIsValid(variations);

        Variations = variations;
        TrackUpdate();
    }

    // Dry up repeated lifecycle code
    private void EnsureNotArchived()
    {
        if (IsArchived)
            throw new FlagDomainException($"Flag '{Name}' is archived and cannot be modified.");
    }

    private void TrackUpdate() => UpdatedAt = DateTime.UtcNow;

    private static void EnsureVariationMenuIsValid(IReadOnlyList<Variation> variations)
    {
        if (variations.Count == 0)
            throw new FlagDomainException("Flag must declare at least one variation.");

        if (variations.Count > 20)
            throw new FlagDomainException(
                $"Flag may not declare more than 20 variations (was {variations.Count})."
            );

        // Optimized allocation-free pass for Kind homogeneity
        VariationKind expectedKind = variations[0].Kind;
        foreach (Variation v in variations)
        {
            if (v.Kind != expectedKind)
                throw new FlagDomainException(
                    $"All variations must share the same Kind (found {expectedKind} and {v.Kind})."
                );
        }

        // Distinct check utilizing HashSets for O(N) performance bounds
        HashSet<string> seenKeys = new(StringComparer.OrdinalIgnoreCase);
        HashSet<string> seenValues = new(StringComparer.Ordinal);

        foreach (Variation v in variations)
        {
            if (!seenKeys.Add(v.Key))
                throw new FlagDomainException(
                    $"Variation key '{v.Key}' is duplicated (case-insensitive)."
                );

            if (!seenValues.Add(v.Value))
                throw new FlagDomainException(
                    $"Variation value '{v.Value}' is duplicated within the menu."
                );
        }
    }
}
