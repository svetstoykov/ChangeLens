namespace ChangeLens.Core.FindingValidation.Models;

/// <summary>
///     Represents one reviewer finding removed during mechanical validation.
/// </summary>
/// <param name="Scope">The kind of item that was removed.</param>
/// <param name="Id">The finding identifier or draft position.</param>
/// <param name="Reason">The stable reason code for the removal.</param>
public sealed record FindingValidationRemoval(string Scope, string Id, string Reason);
