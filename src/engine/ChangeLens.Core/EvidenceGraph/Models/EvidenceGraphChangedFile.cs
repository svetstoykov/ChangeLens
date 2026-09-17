namespace ChangeLens.Core.EvidenceGraph.Models;

/// <summary>
///     Represents the hunk windows collected for one captured changed file.
/// </summary>
internal sealed class EvidenceGraphChangedFile
{
    /// <summary>
    ///     Initializes the state for one changed path.
    /// </summary>
    /// <param name="path">The manifest path that identifies the changed file. Cannot be <see langword="null" />.</param>
    internal EvidenceGraphChangedFile(string path) => this.Path = path;

    /// <summary>
    ///     Gets the manifest path that identifies the changed file.
    /// </summary>
    internal string Path { get; }

    /// <summary>
    ///     Gets or sets a value indicating whether the file produced at least one textual hunk window.
    /// </summary>
    internal bool HasHunk { get; set; }

    /// <summary>
    ///     Gets the hunk windows collected for the file.
    /// </summary>
    internal List<EvidenceGraphWindow> Windows { get; } = [];
}
