namespace ChangeLens.Core.ClaimChecking.Models;

/// <summary>
///     Represents the claims payload sent as the checker's user message.
/// </summary>
/// <param name="Claims">The claims submitted in this call.</param>
internal sealed record CheckerPayload(IReadOnlyList<CheckerClaim> Claims);
