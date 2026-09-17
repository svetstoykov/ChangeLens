using System.Text.RegularExpressions;

namespace ChangeLens.Core.ContextPolicy.Services;

/// <summary>
///     Represents one secret scanner pattern with its display name and value-capture behavior.
/// </summary>
/// <param name="Name">The short pattern name recorded on a match. Cannot be <see langword="null" />.</param>
/// <param name="Regex">The compiled pattern. Cannot be <see langword="null" />.</param>
/// <param name="CapturesValue">Whether an accepted match must pass the captured-value filter.</param>
internal sealed record ContextPolicySecretPattern(string Name, Regex Regex, bool CapturesValue);
