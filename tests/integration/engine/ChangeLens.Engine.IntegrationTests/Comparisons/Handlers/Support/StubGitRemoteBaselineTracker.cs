using ChangeLens.Core.Git.Interfaces;
using ChangeLens.Core.Git.Models;
using ChangeLens.Core.Results.Models;

namespace ChangeLens.Engine.IntegrationTests.Comparisons.Handlers.Support;

/// <summary>
///     Provides one caller-selected remote-baseline check result.
/// </summary>
/// <param name="checkResult">The result returned by every check.</param>
internal sealed class StubGitRemoteBaselineTracker(RemoteBaselineCheckResult checkResult) : IGitRemoteBaselineTracker
{
    /// <inheritdoc />
    public Task<Result<RemoteBaselineCheckResult>> CheckAsync(
        string? path,
        string? target,
        CancellationToken cancellationToken) =>
        Task.FromResult(Result.Success(checkResult));

    /// <inheritdoc />
    /// <exception cref="NotSupportedException">
    ///     The controlled remote-baseline tracker does not refresh.
    /// </exception>
    public Task<Result<string>> RefreshAsync(
        string? path,
        string? target,
        CancellationToken cancellationToken) =>
        throw new NotSupportedException("The controlled remote-baseline tracker does not refresh.");
}
