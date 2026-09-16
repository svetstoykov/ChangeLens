namespace ChangeLens.Core.ChangeAnatomy.Models;

/// <summary>
///     Represents one normalized key extracted from a changed file.
/// </summary>
/// <param name="Normalized">The lower-case normalized key. Cannot be <see langword="null" />.</param>
/// <param name="Kind">The source form that produced the key.</param>
/// <param name="Occurrences">The distinct source occurrences. Cannot be <see langword="null" />.</param>
public sealed record ChangeAnatomyKey(
    string Normalized,
    ChangeAnatomyKeyKind Kind,
    IReadOnlyList<ChangeAnatomyKeyOccurrence> Occurrences);
