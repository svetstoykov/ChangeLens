using ChangeLens.Core.ClaimChecking.Interfaces;
using ChangeLens.Core.ClaimChecking.Models;
using ChangeLens.Core.Results.Models;

namespace ChangeLens.Engine.IntegrationTests.ClaimChecking.Support;

/// <summary>
///     Records submitted claims and returns a controlled checker reply.
/// </summary>
internal sealed class RecordingClaimChecker : IClaimChecker
{
    private readonly Func<IReadOnlyList<CheckerClaim>, ClaimCheckerReply>? _replyFactory;
    private readonly Result<ClaimCheckerReply>? _result;

    /// <summary>
    ///     Initializes a new instance of the <see cref="RecordingClaimChecker" /> class.
    /// </summary>
    /// <param name="replyFactory">The reply factory, or <see langword="null" /> when a fixed result is used.</param>
    /// <param name="result">The fixed result, or <see langword="null" /> when a reply factory is used.</param>
    internal RecordingClaimChecker(
        Func<IReadOnlyList<CheckerClaim>, ClaimCheckerReply>? replyFactory = null,
        Result<ClaimCheckerReply>? result = null)
    {
        this._replyFactory = replyFactory;
        this._result = result;
    }

    /// <summary>
    ///     Gets the claims received by the fake checker.
    /// </summary>
    internal IReadOnlyList<CheckerClaim> Claims { get; private set; } = [];

    /// <inheritdoc />
    public Task<Result<ClaimCheckerReply>> CheckAsync(IReadOnlyList<CheckerClaim> claims, CancellationToken cancellationToken)
    {
        this.Claims = claims;
        if (this._result is not null)
        {
            return Task.FromResult(this._result);
        }

        var reply = this._replyFactory?.Invoke(claims) ?? new ClaimCheckerReply([], null);
        return Task.FromResult<Result<ClaimCheckerReply>>(reply);
    }
}
