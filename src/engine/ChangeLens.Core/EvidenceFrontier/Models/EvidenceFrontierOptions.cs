namespace ChangeLens.Core.EvidenceFrontier.Models;

/// <summary>
///     Represents configurable bounds for evidence frontier publication.
/// </summary>
public sealed class EvidenceFrontierOptions
{
    /// <summary>
    ///     Gets or sets the maximum number of entries returned by the frontier. The default is 80 entries.
    /// </summary>
    public int MaximumEntries { get; set; } = 80;
}
