using ChangeLens.Core.ChangeAnatomy.Models;

namespace ChangeLens.Core.EvidenceGraph.Models;

/// <summary>
///     Represents one citable quote window or manifest fact read from a frozen blob.
/// </summary>
/// <remarks>
///     <para>
///         A quote window cites inclusive one-based lines of the blob in <see cref="ObjectId" />. A manifest fact has no
///         textual hunk, so its <see cref="StartLine" /> and <see cref="EndLine" /> are zero and its text is the fact.
///     </para>
///     <para>
///         <see cref="Path" /> is where the quote lives at its revision, so the Before side of a rename carries the
///         merge-base path.
///     </para>
/// </remarks>
/// <param name="NodeId">The stable node id: <c>m001</c> for manifest facts, <c>n001</c> for windows. Cannot be <see langword="null" />.</param>
/// <param name="Path">The repository-relative path at the quoted revision. Cannot be <see langword="null" />.</param>
/// <param name="Side">The comparison side of the quoted blob.</param>
/// <param name="ObjectId">The captured blob object identifier. Cannot be <see langword="null" />.</param>
/// <param name="StartLine">The first quoted one-based line, or zero for a manifest fact.</param>
/// <param name="EndLine">The last quoted one-based line, inclusive, or zero for a manifest fact.</param>
/// <param name="QuotedText">The quoted lines joined with line feeds, or the fact text. Cannot be <see langword="null" />.</param>
/// <param name="ContentHash">The <c>sha256:</c>-prefixed lower-case hex hash of the UTF-8 quoted text. Cannot be <see langword="null" />.</param>
/// <param name="IsChangedFile">Whether the quoted file is a captured manifest entry rather than a candidate.</param>
/// <param name="Origins">The distinct origins merged into this node in enum order. Cannot be <see langword="null" />.</param>
/// <param name="Salience">The selection weight: changed-line count or summed match contribution; zero for facts.</param>
/// <param name="Truncated">Whether the window was split from a longer window by the per-node line cap.</param>
public sealed record EvidenceNode(
    string NodeId,
    string Path,
    ChangeAnatomySide Side,
    string ObjectId,
    int StartLine,
    int EndLine,
    string QuotedText,
    string ContentHash,
    bool IsChangedFile,
    IReadOnlyList<EvidenceNodeOrigin> Origins,
    double Salience,
    bool Truncated)
{
    /// <summary>
    ///     Gets a value indicating whether this node is a manifest fact rather than a quote window.
    /// </summary>
    public bool IsManifestFact => this.StartLine == 0;
}
