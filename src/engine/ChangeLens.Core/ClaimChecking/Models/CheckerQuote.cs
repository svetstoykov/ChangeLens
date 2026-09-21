using ChangeLens.Core.ChangeAnatomy.Models;

namespace ChangeLens.Core.ClaimChecking.Models;

/// <summary>
///     Represents one binder-held quote supplied with a checker claim.
/// </summary>
/// <param name="NodeId">The evidence node identifier.</param>
/// <param name="Path">The quoted repository-relative path.</param>
/// <param name="Side">The comparison side containing the quote.</param>
/// <param name="StartLine">The first quoted line, or zero for a manifest fact.</param>
/// <param name="EndLine">The last quoted line, or zero for a manifest fact.</param>
/// <param name="Role">The relationship endpoint role, or <see langword="null" /> for other claims.</param>
/// <param name="NumberedText">The quote text with absolute line numbers.</param>
public sealed record CheckerQuote(
    string NodeId,
    string Path,
    ChangeAnatomySide Side,
    int StartLine,
    int EndLine,
    string? Role,
    string NumberedText);
