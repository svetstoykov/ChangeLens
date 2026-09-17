using ChangeLens.Core.ChangeAnatomy.Models;

namespace ChangeLens.Core.EvidenceGraph.Models;

/// <summary>
///     Represents a quote window while the evidence graph is assembled.
/// </summary>
/// <remarks>
///     A window keeps the path at its quoted revision and, for a captured changed file, the manifest path that groups
///     it into one changed file. Windows are split and merged before selection, so the range, salience, and origins
///     change during construction.
/// </remarks>
internal sealed class EvidenceGraphWindow
{
    private readonly HashSet<EvidenceNodeOrigin> _origins;

    /// <summary>
    ///     Initializes a window with the given range and origins.
    /// </summary>
    /// <param name="path">The repository-relative path at the quoted revision. Cannot be <see langword="null" />.</param>
    /// <param name="changedPath">
    ///     The manifest path that groups the window, or <see langword="null" /> for a candidate window.
    /// </param>
    /// <param name="side">The comparison side of the quoted blob.</param>
    /// <param name="objectId">The captured blob object identifier. Cannot be <see langword="null" />.</param>
    /// <param name="start">The first quoted one-based line.</param>
    /// <param name="end">The last quoted one-based line, inclusive.</param>
    /// <param name="origins">The origins to record. Cannot be <see langword="null" />.</param>
    /// <param name="salience">The initial selection weight.</param>
    /// <param name="truncated">Whether the window was split by the per-node line cap.</param>
    internal EvidenceGraphWindow(
        string path,
        string? changedPath,
        ChangeAnatomySide side,
        string objectId,
        int start,
        int end,
        IReadOnlyCollection<EvidenceNodeOrigin> origins,
        double salience,
        bool truncated)
    {
        this.Path = path;
        this.ChangedPath = changedPath;
        this.Side = side;
        this.ObjectId = objectId;
        this.Start = start;
        this.End = end;
        this._origins = [.. origins];
        this.Salience = salience;
        this.Truncated = truncated;
    }

    /// <summary>
    ///     Gets the repository-relative path at the quoted revision.
    /// </summary>
    internal string Path { get; }

    /// <summary>
    ///     Gets the manifest path that groups the window, or <see langword="null" /> for a candidate window.
    /// </summary>
    internal string? ChangedPath { get; }

    /// <summary>
    ///     Gets the comparison side of the quoted blob.
    /// </summary>
    internal ChangeAnatomySide Side { get; }

    /// <summary>
    ///     Gets the captured blob object identifier.
    /// </summary>
    internal string ObjectId { get; }

    /// <summary>
    ///     Gets or sets the first quoted one-based line.
    /// </summary>
    internal int Start { get; set; }

    /// <summary>
    ///     Gets or sets the last quoted one-based line, inclusive.
    /// </summary>
    internal int End { get; set; }

    /// <summary>
    ///     Gets or sets the selection weight.
    /// </summary>
    internal double Salience { get; set; }

    /// <summary>
    ///     Gets or sets a value indicating whether the window was split by the per-node line cap.
    /// </summary>
    internal bool Truncated { get; set; }

    /// <summary>
    ///     Gets the distinct origins recorded for the window.
    /// </summary>
    internal IReadOnlyCollection<EvidenceNodeOrigin> Origins => this._origins;

    /// <summary>
    ///     Gets the number of quoted lines, inclusive of both ends.
    /// </summary>
    internal int Length => this.End - this.Start + 1;

    /// <summary>
    ///     Determines whether the window quotes the given one-based line.
    /// </summary>
    /// <param name="lineNumber">The one-based line number to test.</param>
    /// <returns><see langword="true" /> when the line lies inside the window; otherwise, <see langword="false" />.</returns>
    internal bool Covers(int lineNumber) => lineNumber >= this.Start && lineNumber <= this.End;

    /// <summary>
    ///     Extends the window over another window, unioning origins and summing salience.
    /// </summary>
    /// <param name="other">The window to merge into this one. Cannot be <see langword="null" />.</param>
    internal void Merge(EvidenceGraphWindow other)
    {
        this.End = Math.Max(this.End, other.End);
        this.Salience += other.Salience;
        this.Truncated |= other.Truncated;
        this._origins.UnionWith(other._origins);
    }
}
