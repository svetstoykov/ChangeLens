using ChangeLens.Core.Comparisons.Interfaces;
using ChangeLens.Core.Comparisons.Models;
using ChangeLens.Core.Results.Models;

namespace ChangeLens.Engine.IntegrationTests.Comparisons.Handlers.Support;

/// <summary>
///     Provides one caller-selected comparison freshness state.
/// </summary>
/// <param name="state">The freshness state returned by every check.</param>
internal sealed class StubGitComparisonFreshnessChecker(ComparisonFreshnessState state) : IGitComparisonFreshnessChecker
{
    /// <inheritdoc />
    public Task<Result<ComparisonFreshnessState>> CheckAsync(
        string? path,
        string? target,
        string? freshnessToken,
        CancellationToken cancellationToken) =>
        Task.FromResult(Result.Success(state));
}
