namespace ChangeLens.Core.DraftValidation.Models;

/// <summary>
///     Represents one curator draft item removed during mechanical validation.
/// </summary>
/// <param name="Scope">The kind of item that was removed.</param>
/// <param name="Id">The item identifier or validation location.</param>
/// <param name="Reason">The rule that removed the item.</param>
public sealed record ValidationRemoval(string Scope, string Id, string Reason);
