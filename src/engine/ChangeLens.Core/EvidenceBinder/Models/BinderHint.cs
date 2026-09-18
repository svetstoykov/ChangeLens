namespace ChangeLens.Core.EvidenceBinder.Models;

/// <summary>
///     Represents developer-provided context that guides attention without proving behavior.
/// </summary>
/// <param name="Text">The bounded developer hint.</param>
/// <param name="Label">The label explaining that the value is not evidence.</param>
/// <param name="IsTruncated">Whether the original hint exceeded its cap.</param>
public sealed record BinderHint(string Text, string Label, bool IsTruncated);
