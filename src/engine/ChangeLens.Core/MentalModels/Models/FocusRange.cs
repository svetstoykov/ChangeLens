namespace ChangeLens.Core.MentalModels.Models;

/// <summary>
///     Represents one inclusive focus range within a quoted evidence node.
/// </summary>
/// <param name="NodeId">The evidence node that contains the range.</param>
/// <param name="StartLine">The one-based first line in the range.</param>
/// <param name="EndLine">The one-based last line in the range.</param>
public sealed record FocusRange(string NodeId, int StartLine, int EndLine);
