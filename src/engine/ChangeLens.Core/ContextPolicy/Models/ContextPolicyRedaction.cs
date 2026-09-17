namespace ChangeLens.Core.ContextPolicy.Models;

/// <summary>
///     Represents one quoted line replaced because it matched a secret pattern.
/// </summary>
/// <param name="LineNumber">The one-based blob line that was replaced.</param>
/// <param name="PatternName">The short name of the secret pattern that matched. Cannot be <see langword="null" />.</param>
public sealed record ContextPolicyRedaction(int LineNumber, string PatternName);
