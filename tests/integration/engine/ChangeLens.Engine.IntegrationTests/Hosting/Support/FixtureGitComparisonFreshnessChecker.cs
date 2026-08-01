using ChangeLens.Core.Comparisons.Interfaces;
using ChangeLens.Core.Comparisons.Models;
using ChangeLens.Core.Repositories.Models;
using ChangeLens.Core.Results.Models;

namespace ChangeLens.Engine.IntegrationTests.Hosting.Support;

/// <summary>
///     Returns current comparison facts for the controlled repositories used by processor hosting tests.
/// </summary>
internal sealed class FixtureGitComparisonFreshnessChecker : IGitComparisonFreshnessChecker
{
    /// <inheritdoc />
    public Task<Result<ComparisonFreshnessCheck>> CheckAsync(
        string? path,
        string? target,
        string? freshnessToken,
        CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        var repository = new RepositoryDescriptor(
            Path.GetFileName(path),
            path,
            new BranchRepositoryHead("main", "0123456789abcdef0123456789abcdef01234567"));
        return Task.FromResult<Result<ComparisonFreshnessCheck>>(
            new ComparisonFreshnessCheck(
                ComparisonFreshnessState.Current,
                repository,
                "89abcdef0123456789abcdef0123456789abcdef"));
    }
}
