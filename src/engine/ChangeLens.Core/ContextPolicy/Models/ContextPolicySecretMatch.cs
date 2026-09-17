namespace ChangeLens.Core.ContextPolicy.Models;

/// <summary>
///     Represents one quoted line that a secret scanner pattern matched.
/// </summary>
/// <param name="LineIndex">The zero-based index of the matched line within the quoted text.</param>
/// <param name="PatternName">The short name of the secret pattern that matched. Cannot be <see langword="null" />.</param>
public sealed record ContextPolicySecretMatch(int LineIndex, string PatternName);
