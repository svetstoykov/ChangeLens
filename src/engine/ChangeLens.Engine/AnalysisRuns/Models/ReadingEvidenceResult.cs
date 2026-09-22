namespace ChangeLens.Engine.AnalysisRuns.Models;

/// <summary>
///     Represents the protocol projection of one disclosed evidence quote.
/// </summary>
/// <param name="NodeId">The evidence node identifier.</param>
/// <param name="Path">The repository-relative path of the quoted file.</param>
/// <param name="Side">The comparison side wire value.</param>
/// <param name="StartLine">The first absolute file line of the quote.</param>
/// <param name="EndLine">The last absolute file line of the quote.</param>
/// <param name="IsChangedFile">Whether the quoted file is part of the change.</param>
/// <param name="IsRedacted">Whether context policy redacted part of the quote.</param>
/// <param name="IsTruncated">Whether context policy truncated the quote.</param>
/// <param name="Text">The quote text the binder disclosed.</param>
internal sealed record ReadingEvidenceResult(
    string NodeId,
    string Path,
    string Side,
    int StartLine,
    int EndLine,
    bool IsChangedFile,
    bool IsRedacted,
    bool IsTruncated,
    string Text);
