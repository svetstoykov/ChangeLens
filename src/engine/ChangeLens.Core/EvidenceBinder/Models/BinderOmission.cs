namespace ChangeLens.Core.EvidenceBinder.Models;

/// <summary>
///     Represents grouped evidence omitted from the binder and why it was omitted.
/// </summary>
/// <param name="Kind">The stable omission kind.</param>
/// <param name="Count">The total omitted item count in the group.</param>
/// <param name="Reason">The omission reason.</param>
/// <param name="Items">The bounded representative item ids or paths.</param>
public sealed record BinderOmission(string Kind, int Count, string Reason, IReadOnlyList<string> Items);
