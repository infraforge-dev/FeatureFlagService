using Banderas.Domain.Enums;
using Banderas.Domain.Exceptions;
using Banderas.Domain.ValueObjects;

namespace Banderas.Domain.Entities;

public class Flag
{
    public Guid Id { get; private set; } = Guid.NewGuid();
    public uint Version { get; private set; }
    public string Name { get; private set; }
    public EnvironmentType Environment { get; private set; }
    public bool IsEnabled { get; private set; }
    public bool IsArchived { get; private set; }
    public RolloutStrategy StrategyType { get; private set; }
    public string? Description { get; private set; }
    public IReadOnlyList<string> Tags { get; private set; } = [];

    private IReadOnlyList<Variation> _variations = [];

    /// <summary>
    /// The ordered, non-empty menu of variations this flag may produce.
    /// Ordering is wire-significant: index is the machine identity referenced
    /// by targeting rules (Phase 5) and SDK telemetry (Phase 7); key is the
    /// human identity for UIs, logs, and AI prompts.
    /// </summary>
    public IReadOnlyList<Variation> Variations
    {
        get => _variations;
        private set => _variations = value;
    }

    private StrategyConfig _strategyConfig = null!;

    public StrategyConfig StrategyConfig
    {
        get
        {
            // EF Core sets _strategyConfig via the converter with ValidatedFor = None.
            // Reconcile with the actual StrategyType for materialized entities.
            if (_strategyConfig.ValidatedFor != StrategyType)
            {
                _strategyConfig = new StrategyConfig(StrategyType, _strategyConfig.RawJson);
            }

            return _strategyConfig;
        }
        private set => _strategyConfig = value;
    }
    public DateTime CreatedAt { get; private set; } = DateTime.UtcNow;
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
        if (string.IsNullOrWhiteSpace(name))
        {
            throw new ArgumentException("Name cannot be empty.", nameof(name));
        }

        if (strategyConfig.ValidatedFor != strategyType)
        {
            throw new FlagDomainException(
                $"StrategyConfig was validated for '{strategyConfig.ValidatedFor}' "
                    + $"but Flag strategy is '{strategyType}'."
            );
        }

        ArgumentNullException.ThrowIfNull(variations);
        EnsureVariationMenuIsValid(variations);

        Name = name;
        Environment = environment;
        IsEnabled = isEnabled;
        StrategyType = strategyType;
        StrategyConfig = strategyConfig;
        Description = description;
        Tags = tags ?? [];
        _variations = variations;
    }

    // Required by EF Core
    private Flag()
    {
        Name = string.Empty;
        StrategyConfig = new StrategyConfig(RolloutStrategy.None, "{}");
        Tags = [];
        _variations = [];
    }

    public void UpdateName(string name)
    {
        if (IsArchived)
        {
            throw new FlagDomainException($"Flag '{Name}' is archived and cannot be modified.");
        }

        if (string.IsNullOrWhiteSpace(name))
        {
            throw new ArgumentException("Name cannot be empty.", nameof(name));
        }

        Name = name;
        UpdatedAt = DateTime.UtcNow;
    }

    /// <summary>
    /// Atomically replaces the rollout configuration (enabled state, strategy type,
    /// and strategy config) in a single operation, setting UpdatedAt exactly once.
    /// </summary>
    public void Reconfigure(
        bool isEnabled,
        RolloutStrategy strategyType,
        StrategyConfig strategyConfig
    )
    {
        if (IsArchived)
        {
            throw new FlagDomainException($"Flag '{Name}' is archived and cannot be modified.");
        }

        if (strategyConfig.ValidatedFor != strategyType)
        {
            throw new FlagDomainException(
                $"StrategyConfig was validated for '{strategyConfig.ValidatedFor}' "
                    + $"but Flag strategy is '{strategyType}'."
            );
        }

        IsEnabled = isEnabled;
        StrategyType = strategyType;
        StrategyConfig = strategyConfig;
        UpdatedAt = DateTime.UtcNow;
    }

    public void Archive()
    {
        if (IsArchived)
        {
            throw new FlagDomainException($"Flag '{Name}' is archived and cannot be modified.");
        }

        IsArchived = true;
        ArchivedAt = DateTime.UtcNow;
        UpdatedAt = DateTime.UtcNow;
    }

    public void UpdateMetadata(string? description, IReadOnlyList<string> tags)
    {
        if (IsArchived)
        {
            throw new FlagDomainException($"Flag '{Name}' is archived and cannot be modified.");
        }

        ArgumentNullException.ThrowIfNull(tags);

        Description = description;
        Tags = tags;
        UpdatedAt = DateTime.UtcNow;
    }

    /// <summary>
    /// Atomically replaces the variation menu with <paramref name="variations"/>.
    /// All-or-nothing: the collection-level invariants are checked first, and on
    /// failure the existing menu is preserved. No partial-application semantics.
    /// </summary>
    public void UpdateVariations(IReadOnlyList<Variation> variations)
    {
        if (IsArchived)
        {
            throw new FlagDomainException($"Flag '{Name}' is archived and cannot be modified.");
        }

        ArgumentNullException.ThrowIfNull(variations);
        EnsureVariationMenuIsValid(variations);

        _variations = variations;
        UpdatedAt = DateTime.UtcNow;
    }

    /// <summary>
    /// Enforces the five collection-level variation invariants (non-empty, max 20,
    /// shared Kind, unique keys case-insensitive, unique values ordinal).
    /// Single-element invariants live on the <see cref="Variation"/> VO ctor.
    /// </summary>
    private static void EnsureVariationMenuIsValid(IReadOnlyList<Variation> variations)
    {
        if (variations.Count == 0)
        {
            throw new FlagDomainException("Flag must declare at least one variation.");
        }

        if (variations.Count > 20)
        {
            throw new FlagDomainException(
                $"Flag may not declare more than 20 variations (was {variations.Count})."
            );
        }

        VariationKind firstKind = variations[0].Kind;
        for (int i = 1; i < variations.Count; i++)
        {
            if (variations[i].Kind != firstKind)
            {
                throw new FlagDomainException(
                    "All variations on a flag must share the same Kind "
                        + $"(found {firstKind} and {variations[i].Kind})."
                );
            }
        }

        HashSet<string> seenKeys = new(StringComparer.OrdinalIgnoreCase);
        foreach (Variation v in variations)
        {
            if (!seenKeys.Add(v.Key))
            {
                throw new FlagDomainException(
                    $"Variation key '{v.Key}' is duplicated (keys are case-insensitive)."
                );
            }
        }

        HashSet<string> seenValues = new(StringComparer.Ordinal);
        foreach (Variation v in variations)
        {
            if (!seenValues.Add(v.Value))
            {
                throw new FlagDomainException(
                    $"Variation value '{v.Value}' is duplicated within the menu."
                );
            }
        }
    }
}
