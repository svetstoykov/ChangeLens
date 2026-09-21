using ChangeLens.Core.ClaimChecking.Models;
using ChangeLens.Core.Results.Models;

namespace ChangeLens.Core.ClaimChecking.Interfaces;

/// <summary>
///     Defines the provider-neutral port used to check built claims.
/// </summary>
public interface IClaimChecker
{
    /// <summary>
    ///     Asynchronously checks the supplied claims.
    /// </summary>
    /// <param name="claims">The claims and isolated quotes to check.</param>
    /// <param name="cancellationToken">The cancellation token to observe.</param>
    /// <returns>A task whose result contains raw checker verdicts or a failure.</returns>
    Task<Result<ClaimCheckerReply>> CheckAsync(IReadOnlyList<CheckerClaim> claims, CancellationToken cancellationToken);
}
