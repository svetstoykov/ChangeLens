namespace ChangeLens.Core.EvidenceGraph.Models;

/// <summary>
///     Represents an inclusive range of one-based lines on one side of a quoted blob.
/// </summary>
/// <param name="StartLine">The first line of the range.</param>
/// <param name="EndLine">The last line of the range, inclusive.</param>
public sealed record EvidenceLineRange(int StartLine, int EndLine);
