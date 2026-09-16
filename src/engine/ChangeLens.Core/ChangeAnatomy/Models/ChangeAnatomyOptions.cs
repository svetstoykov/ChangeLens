namespace ChangeLens.Core.ChangeAnatomy.Models;

/// <summary>
///     Represents configurable bounds for deterministic change anatomy extraction.
/// </summary>
public sealed class ChangeAnatomyOptions
{
    /// <summary>
    ///     Gets or sets the shortest normalized key to retain. The default is three characters.
    /// </summary>
    public int MinimumKeyLength { get; set; } = 3;

    /// <summary>
    ///     Gets or sets the maximum number of distinct keys retained per changed file. The default is 400 keys.
    /// </summary>
    public int MaximumKeysPerFile { get; set; } = 400;

    /// <summary>
    ///     Gets or sets the maximum occurrences retained for one key in a file. The default is eight occurrences.
    /// </summary>
    public int MaximumOccurrencesPerKey { get; set; } = 8;
}
