using ChangeLens.Core.Comparisons.Interfaces;
using ChangeLens.Core.Comparisons.Models;
using ChangeLens.Core.Results.Models;

namespace ChangeLens.Engine.IntegrationTests.Comparisons.Handlers.Support;

/// <summary>
///     Provides one caller-selected comparison freshness check.
/// </summary>
/// <param name="check">The freshness check returned by every check.</param>
internal sealed class StubGitComparisonFreshnessChecker(ComparisonFreshnessCheck check) : IGitComparisonFreshnessChecker
{
    /// <inheritdoc />
    public Task<Result<ComparisonFreshnessCheck>> CheckAsync(
        string? path,
        string? target,
        string? freshnessToken,
        CancellationToken cancellationToken) =>
        Task.FromResult(Result.Success(check));
}
