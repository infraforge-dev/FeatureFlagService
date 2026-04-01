using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;
using Microsoft.OpenApi.Models;

namespace FeatureFlag.Api.Domain
{
    /// <summary>
    /// Represents a feature flag used to control feature rollout in different environments.
    /// </summary>
    public class FeatureFlag
    {
        [Key]
        [Required]
        [JsonPropertyName("id")]
        [Display(Name = "Feature Flag ID", Description = "Unique identifier for the feature flag.")]
        public Guid Id { get; private set; } = Guid.NewGuid();

        [Required]
        [StringLength(100, MinimumLength = 3)]
        [JsonPropertyName("name")]
        [Display(Name = "Feature Name", Description = "Unique name of the feature.")]
        public string Name { get; private set; }

        [Required]
        [JsonPropertyName("environment")]
        [Display(Name = "Environment", Description = "Environment where this feature flag applies (e.g., Development, Staging, Production).")]
        public EnvironmentType Environment { get; private set; }

        [Required]
        [JsonPropertyName("isEnabled")]
        [Display(Name = "Is Enabled", Description = "Global toggle to enable or disable the feature.")]
        public bool IsEnabled { get; private set; }

        [Required]
        [JsonPropertyName("strategyType")]
        [Display(Name = "Rollout Strategy", Description = "Strategy used to evaluate if the feature is enabled for a user.")]
        public RolloutStrategy StrategyType { get; private set; }

        [Required]
        [JsonPropertyName("strategyConfig")]
        [Display(Name = "Strategy Configuration", Description = "JSON string containing strategy-specific configuration (e.g., percentage, roles).")]
        public string StrategyConfig { get; private set; }

        [Required]
        [JsonPropertyName("createdAt")]
        [Display(Name = "Created At", Description = "UTC timestamp when the feature flag was created.")]
        public DateTime CreatedAt { get; private set; } = DateTime.UtcNow;

        [Required]
        [JsonPropertyName("updatedAt")]
        [Display(Name = "Updated At", Description = "UTC timestamp when the feature flag was last updated.")]
        public DateTime UpdatedAt { get; private set; } = DateTime.UtcNow;

        /// <summary>
        /// Creates a new feature flag.
        /// </summary>
        public FeatureFlag(
            string name,
            EnvironmentType environment,
            bool isEnabled,
            RolloutStrategy strategyType,
            string strategyConfig)
        {
            Name = name ?? throw new ArgumentNullException(nameof(name));
            Environment = environment;
            IsEnabled = isEnabled;
            StrategyType = strategyType;
            StrategyConfig = strategyConfig ?? "{}";
        }

        /// <summary>
        /// Sets the global enabled/disabled state.
        /// </summary>
        public void SetEnabled(bool enabled)
        {
            IsEnabled = enabled;
            UpdatedAt = DateTime.UtcNow;
        }

        /// <summary>
        /// Updates the rollout strategy and its configuration.
        /// </summary>
        public void UpdateStrategy(RolloutStrategy strategyType, string strategyConfig)
        {
            StrategyType = strategyType;
            StrategyConfig = strategyConfig ?? "{}";
            UpdatedAt = DateTime.UtcNow;
        }
    }
}