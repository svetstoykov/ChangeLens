using ChangeLens.Core.AnalysisRuns.Models;
using ChangeLens.Core.Git.Interfaces;
using ChangeLens.Core.Results.Models;
using ChangeLens.Core.Snapshots.Constants;
using ChangeLens.Core.Snapshots.Interfaces;
using ChangeLens.Core.Snapshots.Models;
using Microsoft.Extensions.Logging;

namespace ChangeLens.Core.Snapshots.Services;

/// <summary>
///     Creates readers whose Git operations are scoped to one captured snapshot.
/// </summary>
/// <param name="commandRunner">The binary-safe installed Git command runner. Cannot be <see langword="null" />.</param>
/// <param name="options">The configured frozen-read bounds. Cannot be <see langword="null" />.</param>
/// <param name="loggerFactory">The logger factory for frozen-read outcomes. Cannot be <see langword="null" />.</param>
/// <exception cref="ArgumentNullException">
///     <paramref name="commandRunner" />, <paramref name="options" />, or <paramref name="loggerFactory" /> is
///     <see langword="null" />.
/// </exception>
public sealed class FrozenGitTreeReaderFactory(
    IGitBinaryCommandRunner commandRunner,
    FrozenGitTreeReaderOptions options,
    ILoggerFactory loggerFactory) : IFrozenGitTreeReaderFactory
{
    private readonly IGitBinaryCommandRunner _commandRunner = commandRunner ?? throw new ArgumentNullException(nameof(commandRunner));
    private readonly FrozenGitTreeReaderOptions _options = options ?? throw new ArgumentNullException(nameof(options));
    private readonly ILoggerFactory _loggerFactory = loggerFactory ?? throw new ArgumentNullException(nameof(loggerFactory));

    /// <inheritdoc />
    public Result<IFrozenGitTreeReader> Open(
        AnalysisRepositoryIdentity repository,
        SnapshotManifest snapshot)
    {
        ArgumentNullException.ThrowIfNull(repository);
        ArgumentNullException.ThrowIfNull(snapshot);

        if (!StringComparer.Ordinal.Equals(repository.CanonicalRepositoryPathKey, snapshot.CanonicalRepositoryPathKey)
            || !StringComparer.Ordinal.Equals(repository.HeadRevision, snapshot.HeadRevision)
            || string.IsNullOrWhiteSpace(repository.CanonicalPath)
            || repository.CanonicalPath.Contains('\0')
            || !IsRevision(repository.HeadRevision, repository.HeadRevision.Length)
            || !IsRevision(snapshot.TargetRevision, repository.HeadRevision.Length)
            || !IsRevision(snapshot.HeadRevision, repository.HeadRevision.Length)
            || !IsRevision(snapshot.MergeBaseRevision, repository.HeadRevision.Length)
            || snapshot.Entries.Any(entry => !IsObjectIdentity(entry.MergeBaseObjectId, repository.HeadRevision.Length)
                || !IsObjectIdentity(entry.HeadObjectId, repository.HeadRevision.Length))
            || !ArePositive(_options))
        {
            return OperationError.Validation(
                "The captured snapshot cannot be opened for frozen Git reads.",
                SnapshotErrorCode.InvalidSnapshot);
        }

        return Result.Success<IFrozenGitTreeReader>(
            new FrozenGitTreeReader(
                this._commandRunner,
                repository,
                snapshot,
                this._options,
                this._loggerFactory.CreateLogger<FrozenGitTreeReader>()));
    }

    private static bool ArePositive(FrozenGitTreeReaderOptions options) =>
        options.MaximumBlobBytes > 0
        && options.MaximumTreeFiles > 0
        && options.MaximumHistoryCommits > 0
        && options.MaximumHistoryPathsPerCommit > 0
        && options.CommandTimeout > TimeSpan.Zero;

    private static bool IsRevision(string value, int expectedLength) =>
        IsHexIdentity(value, expectedLength)
        && value.Any(static character => character != '0');

    private static bool IsObjectIdentity(string value, int expectedLength) =>
        IsHexIdentity(value, expectedLength);

    private static bool IsHexIdentity(string value, int expectedLength) =>
        expectedLength is 40 or 64
        && !string.IsNullOrWhiteSpace(value)
        && value.Length == expectedLength
        && value.All(static character => Uri.IsHexDigit(character));
}
