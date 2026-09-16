using ChangeLens.Core.ChangeAnatomy.Models;

namespace ChangeLens.Core.Correspondence.Services;

/// <summary>
///     Represents one normalized key indexed for a single captured tree file.
/// </summary>
internal sealed class CorrespondenceIndexedToken
{
    private readonly HashSet<ChangeAnatomyKeyKind> _kinds = [];
    private readonly List<int> _lines = [];

    /// <summary>
    ///     Initializes an indexed token for a normalized key.
    /// </summary>
    /// <param name="normalized">The lower-case normalized key. Cannot be <see langword="null" />.</param>
    /// <exception cref="ArgumentNullException"><paramref name="normalized" /> is <see langword="null" />.</exception>
    internal CorrespondenceIndexedToken(string normalized)
    {
        ArgumentNullException.ThrowIfNull(normalized);
        this.Normalized = normalized;
    }

    /// <summary>
    ///     Gets the lower-case normalized key.
    /// </summary>
    internal string Normalized { get; }

    /// <summary>
    ///     Gets the distinct key kinds the token carries in the file.
    /// </summary>
    internal IReadOnlySet<ChangeAnatomyKeyKind> Kinds => this._kinds;

    /// <summary>
    ///     Gets the ascending distinct one-based lines holding the key in the file.
    /// </summary>
    internal IReadOnlyList<int> Lines => this._lines;

    /// <summary>
    ///     Records one key kind, ignoring duplicates.
    /// </summary>
    /// <param name="kind">The key kind to record.</param>
    internal void AddKind(ChangeAnatomyKeyKind kind) => this._kinds.Add(kind);

    /// <summary>
    ///     Records one source line unless the occurrence bound has been reached or the line is already present.
    /// </summary>
    /// <param name="lineNumber">The one-based source line.</param>
    /// <param name="maximumLines">The maximum distinct lines to retain.</param>
    internal void AddLine(int lineNumber, int maximumLines)
    {
        if (this._lines.Count >= maximumLines || this._lines.Contains(lineNumber))
        {
            return;
        }

        this._lines.Add(lineNumber);
    }
}
