using ChangeLens.Core.ChangeAnatomy.Models;
using ChangeLens.Core.EvidenceGraph.Models;

namespace ChangeLens.Core.ContextPolicy.Models;

/// <summary>
///     Represents an evidence node that context policy allows to leave the engine.
/// </summary>
/// <param name="NodeId">The evidence node id. Cannot be <see langword="null" />.</param>
/// <param name="Path">The repository-relative path at the quoted revision. Cannot be <see langword="null" />.</param>
/// <param name="Side">The comparison side of the quoted blob.</param>
/// <param name="ObjectId">The captured blob object identifier. Cannot be <see langword="null" />.</param>
/// <param name="StartLine">The first quoted one-based line, or zero for a manifest fact.</param>
/// <param name="EndLine">The last quoted one-based line, inclusive, or zero for a manifest fact.</param>
/// <param name="IsChangedFile">Whether the quoted file is a captured manifest entry rather than a candidate.</param>
/// <param name="Origins">The distinct origins of the node in enum order. Cannot be <see langword="null" />.</param>
/// <param name="Salience">The selection weight carried from the graph.</param>
/// <param name="Text">The disclosed text, with redacted lines replaced. Cannot be <see langword="null" />.</param>
/// <param name="ContentHash">The hash of the original quoted text. Cannot be <see langword="null" />.</param>
/// <param name="DisclosedHash">The hash of <paramref name="Text" /> when redacted; otherwise <see langword="null" />.</param>
/// <param name="Truncated">Whether the window was split by the per-node line cap.</param>
public sealed record DisclosedEvidenceNode(
    string NodeId,
    string Path,
    ChangeAnatomySide Side,
    string ObjectId,
    int StartLine,
    int EndLine,
    bool IsChangedFile,
    IReadOnlyList<EvidenceNodeOrigin> Origins,
    double Salience,
    string Text,
    string ContentHash,
    string? DisclosedHash,
    bool Truncated)
{
    /// <summary>
    ///     Gets a value indicating whether one or more lines of <see cref="Text" /> were redacted.
    /// </summary>
    public bool IsRedacted => this.DisclosedHash is not null;
}
