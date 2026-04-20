using System.Text.RegularExpressions;

namespace Banderas.Application.AI;

public sealed partial class PromptSanitizer : IPromptSanitizer
{
    private static readonly string[] DangerousPhrases =
    [
        "ignore previous", "ignore all", "disregard", "you are now",
        "new instruction", "system:", "<s>", "<user>", "<assistant>", "###"
    ];

    private const int MaxLength = 500;

    [GeneratedRegex(@"[\r\n]+")]
    private static partial Regex NewlinePattern();

    public string Sanitize(string input)
    {
        if (string.IsNullOrWhiteSpace(input))
        {
            return string.Empty;
        }

        string sanitized = NewlinePattern().Replace(input, " ");

        foreach (string phrase in DangerousPhrases)
        {
            sanitized = sanitized.Replace(
                phrase, "[REDACTED]",
                StringComparison.OrdinalIgnoreCase);
        }

        if (sanitized.Length > MaxLength)
        {
            sanitized = sanitized[..MaxLength];
        }

        return sanitized.Trim();
    }
}
