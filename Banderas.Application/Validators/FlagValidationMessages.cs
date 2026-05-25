namespace Banderas.Application.Validators;

/// <summary>
/// Strongly-typed validation messages for flag and evaluation request validators.
/// Using a <c>readonly record struct</c> groups related messages into a single
/// type-safe unit with zero heap allocation.
/// </summary>
public readonly record struct FlagValidationMessage(string Message)
{
    public static FlagValidationMessage NameRequired => new("Flag name is required.");

    public static FlagValidationMessage NameTooLong =>
        new("Flag name must not exceed 100 characters.");

    public static FlagValidationMessage NameInvalidCharacters =>
        new("Flag name may only contain letters, numbers, hyphens, and underscores.");

    public static FlagValidationMessage StrategyTypeInvalid =>
        new("StrategyType must be a valid value (None, Percentage, or RoleBased).");

    public static FlagValidationMessage StrategyConfigTooLong =>
        new("StrategyConfig must not exceed 2000 characters.");

    public static FlagValidationMessage StrategyConfigInvalid =>
        new(
            "StrategyConfig is invalid for the specified StrategyType. "
                + "Percentage requires a 'percentage' field (1-100). "
                + "RoleBased requires a non-empty 'roles' array. "
                + "None requires no config."
        );

    public static FlagValidationMessage DescriptionTooLong =>
        new("Description must not exceed 500 characters.");

    public static FlagValidationMessage TagsTooMany =>
        new("Tags may not contain more than 20 entries.");

    public static FlagValidationMessage TagTooLong =>
        new("Each tag must not exceed 50 characters.");

    public static FlagValidationMessage TagInvalidCharacters =>
        new("Tags may only contain lowercase letters, numbers, hyphens, and underscores.");

    public static FlagValidationMessage EvaluationFlagNameRequired => new("FlagName is required.");

    public static FlagValidationMessage EvaluationFlagNameTooLong =>
        new("FlagName must not exceed 100 characters.");

    public static FlagValidationMessage EvaluationUserIdRequired => new("UserId is required.");

    public static FlagValidationMessage EvaluationUserIdTooLong =>
        new("UserId must not exceed 256 characters.");

    public static FlagValidationMessage EvaluationUserRolesNull =>
        new("UserRoles must not be null. Pass an empty array if the user has no roles.");

    public static FlagValidationMessage EvaluationUserRolesTooMany =>
        new("UserRoles must not exceed 50 entries.");

    public static FlagValidationMessage EvaluationRoleTooLong =>
        new("Each role must not exceed 100 characters.");
}
