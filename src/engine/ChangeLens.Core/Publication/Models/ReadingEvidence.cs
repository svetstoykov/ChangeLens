using ChangeLens.Core.ChangeAnatomy.Models;

namespace ChangeLens.Core.Publication.Models;

/// <summary>Represents one distinct evidence node used by published citations.</summary>
/// <param name="NodeId">The evidence node identifier.</param>
/// <param name="Path">The quoted path.</param>
/// <param name="Side">The comparison side.</param>
/// <param name="StartLine">The full quote start line.</param>
/// <param name="EndLine">The full quote end line.</param>
/// <param name="IsChangedFile">Whether this is a changed-file node.</param>
/// <param name="IsRedacted">Whether the disclosed text contains redactions.</param>
/// <param name="IsTruncated">Whether the quote was split by a line cap.</param>
/// <param name="Text">The binder-held text.</param>
public sealed record ReadingEvidence(
    string NodeId,
    string Path,
    ChangeAnatomySide Side,
    int StartLine,
    int EndLine,
    bool IsChangedFile,
    bool IsRedacted,
    bool IsTruncated,
    string Text);
