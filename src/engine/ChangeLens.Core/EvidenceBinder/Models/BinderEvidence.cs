using ChangeLens.Core.ChangeAnatomy.Models;
using ChangeLens.Core.EvidenceGraph.Models;

namespace ChangeLens.Core.EvidenceBinder.Models;

/// <summary>
///     Represents one disclosed quote or manifest fact available to the curator.
/// </summary>
/// <param name="NodeId">The stable evidence node id.</param>
/// <param name="Side">The comparison side containing the quote.</param>
/// <param name="Path">The path at the quoted revision.</param>
/// <param name="StartLine">The first quoted line, or zero for a manifest fact.</param>
/// <param name="EndLine">The last quoted line, or zero for a manifest fact.</param>
/// <param name="IsChangedFile">Whether the evidence belongs to a changed manifest entry.</param>
/// <param name="Origins">The deterministic reasons the node was selected.</param>
/// <param name="Rank">The one-based binder order after budget drops.</param>
/// <param name="Text">The disclosed quote or manifest fact.</param>
/// <param name="ContentHash">The original quote hash.</param>
/// <param name="DisclosedHash">The redacted quote hash, or <see langword="null" />.</param>
/// <param name="IsRedacted">Whether the disclosed text contains redaction markers.</param>
/// <param name="IsTruncated">Whether the graph split the original window.</param>
public sealed record BinderEvidence(
    string NodeId,
    ChangeAnatomySide Side,
    string Path,
    int StartLine,
    int EndLine,
    bool IsChangedFile,
    IReadOnlyList<EvidenceNodeOrigin> Origins,
    int Rank,
    string Text,
    string ContentHash,
    string? DisclosedHash,
    bool IsRedacted,
    bool IsTruncated);
