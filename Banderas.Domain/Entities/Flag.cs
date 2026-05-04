using Banderas.Domain.Enums;
using Banderas.Domain.Exceptions;

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
    public string StrategyConfig { get; private set; }
    public DateTime CreatedAt { get; private set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; private set; } = DateTime.UtcNow;
    public DateTime? ArchivedAt { get; private set; }

    public Flag(
        string name,
        EnvironmentType environment,
        bool isEnabled,
        RolloutStrategy strategyType,
        string? strategyConfig
    )
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            throw new ArgumentException("Name cannot be empty.", nameof(name));
        }

        Name = name;
        Environment = environment;
        IsEnabled = isEnabled;
        StrategyType = strategyType;
        StrategyConfig = strategyConfig ?? "{}";
    }

    // Required by EF Core
    private Flag()
    {
        Name = string.Empty;
        StrategyConfig = "{}";
    }

    public void SetEnabled(bool enabled)
    {
        if (IsArchived)
        {
            throw new FlagDomainException($"Flag '{Name}' is archived and cannot be modified.");
        }

        IsEnabled = enabled;
        UpdatedAt = DateTime.UtcNow;
    }

    public void UpdateStrategy(RolloutStrategy strategyType, string? strategyConfig)
    {
        if (IsArchived)
        {
            throw new FlagDomainException($"Flag '{Name}' is archived and cannot be modified.");
        }

        StrategyType = strategyType;
        StrategyConfig = strategyConfig ?? "{}";
        UpdatedAt = DateTime.UtcNow;
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
    /// Atomically updates the enabled state and rollout strategy in a single
    /// operation, setting UpdatedAt exactly once.
    /// </summary>
    public void Update(bool isEnabled, RolloutStrategy strategyType, string? strategyConfig)
    {
        if (IsArchived)
        {
            throw new FlagDomainException($"Flag '{Name}' is archived and cannot be modified.");
        }

        IsEnabled = isEnabled;
        StrategyType = strategyType;
        StrategyConfig = strategyConfig ?? "{}";
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
}
