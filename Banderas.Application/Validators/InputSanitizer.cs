namespace Banderas.Application.Validators;

/// <summary>
/// Shared input sanitization helper.
/// Called by validators (via RuleFor lambdas) and by the service layer before
/// string values are used in evaluation logic.
///
/// Sanitizes for the HTTP boundary only. Does not substitute for prompt
/// sanitization (Phase 1.5: IPromptSanitizer) or structured logging conventions.
/// </summary>
internal static class InputSanitizer
{
    /// <summary>
    /// Trims leading/trailing whitespace and removes ASCII control characters
    /// (codepoints below 0x20, except tab). Returns null if input is null.
    /// </summary>
    internal static string? Clean(string? value)
    {
        if (value is null)
        {
            return null;
        }

        // Strip control characters (0x00–0x1F) except tab (0x09)
        string cleaned = new string(value.Where(c => c == '\t' || c >= 0x20).ToArray());

        return cleaned.Trim();
    }

    /// <summary>
    /// Applies Clean() to each element. Removes entries that are null or
    /// empty after cleaning.
    /// </summary>
    internal static IEnumerable<string> CleanCollection(IEnumerable<string>? values)
    {
        if (values is null)
        {
            return [];
        }

        return values.Select(Clean).Where(v => !string.IsNullOrEmpty(v)).Cast<string>();
    }
}
