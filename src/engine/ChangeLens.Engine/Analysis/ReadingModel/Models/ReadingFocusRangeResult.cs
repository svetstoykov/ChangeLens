namespace ChangeLens.Engine.Analysis.ReadingModel.Models;

/// <summary>
///     Represents one focus range inside a cited quote, in absolute file lines.
/// </summary>
/// <param name="NodeId">The evidence node identifier the range addresses.</param>
/// <param name="StartLine">The first absolute file line of the range.</param>
/// <param name="EndLine">The last absolute file line of the range.</param>
internal sealed record ReadingFocusRangeResult(string NodeId, int StartLine, int EndLine);
