namespace Banderas.Application.AI;

/// <summary>
/// Sanitizes user-controlled string values before they are embedded in
/// prompts sent to Azure OpenAI. Defends against prompt injection attacks.
/// </summary>
public interface IPromptSanitizer
{
    /// <summary>
    /// Sanitizes a single string value for safe embedding in a prompt.
    /// Strips or neutralizes sequences that could be interpreted as model instructions.
    /// </summary>
    string Sanitize(string input);
}
