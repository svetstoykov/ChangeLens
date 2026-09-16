namespace ChangeLens.Core.ChangeAnatomy.Models;

/// <summary>
///     Represents one occurrence of a change anatomy key.
/// </summary>
/// <param name="Side">The comparison side containing the occurrence.</param>
/// <param name="LineNumber">The one-based changed line number, or <see langword="null" /> for a path key.</param>
/// <param name="Original">The original spelling retained for display. Cannot be <see langword="null" />.</param>
public sealed record ChangeAnatomyKeyOccurrence(ChangeAnatomySide Side, int? LineNumber, string Original);
