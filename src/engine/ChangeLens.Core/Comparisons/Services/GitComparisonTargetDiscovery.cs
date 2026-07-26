using System.Buffers;
using System.Diagnostics;
using System.Security.Cryptography;
using System.Text;
using ChangeLens.Core.Comparisons.Constants;
using ChangeLens.Core.Comparisons.Interfaces;
using ChangeLens.Core.Comparisons.Models;
using ChangeLens.Core.Git.Interfaces;
using ChangeLens.Core.Git.Models;
using ChangeLens.Core.Git.Parsers;
using ChangeLens.Core.Git.Services;
using ChangeLens.Core.Repositories.Models;
using ChangeLens.Core.Results.Models;

namespace ChangeLens.Core.Comparisons.Services;

/// <summary>
///     Discovers supported local and cached remote-tracking comparison targets.
/// </summary>
/// <remarks>
///     <para>
///         The Engine host registers this stateless service as a singleton. It is safe to call concurrently.
///     </para>
///     <para>
///         Discovery inspects local Git metadata only and applies one shared deadline to repository and ref facts.
///     </para>
/// </remarks>
/// <param name="repositoryInspector">The repository inspection service. Cannot be <see langword="null" />.</param>
/// <param name="commandRunner">The controlled Git process boundary. Cannot be <see langword="null" />.</param>
/// <exception cref="ArgumentNullException">
///     <paramref name="repositoryInspector" /> is <see langword="null" />.
///     -or-
///     <paramref name="commandRunner" /> is <see langword="null" />.
/// </exception>
public sealed class GitComparisonTargetDiscovery(
    IGitRepositoryInspector repositoryInspector,
    IGitCommandRunner commandRunner)
    : IGitComparisonTargetDiscovery
{
    private static readonly OperationError TimedOutError = OperationError.Timeout(
        "Comparison inspection exceeded its allowed time.",
        ComparisonErrorCode.TimedOut);

    private static readonly OperationError TooLargeError = OperationError.UnprocessableInput(
        "The comparison exceeds the supported local inspection limit.",
        ComparisonErrorCode.TooLarge);

    private static readonly OperationError InspectionFailedError =
        OperationError.ExternalDependencyFailure(
            "Git comparison inspection failed.",
            ComparisonErrorCode.InspectionFailed);

    private readonly IGitRepositoryInspector _repositoryInspector =
        repositoryInspector ?? throw new ArgumentNullException(nameof(repositoryInspector));

    private readonly IGitCommandRunner _commandRunner =
        commandRunner ?? throw new ArgumentNullException(nameof(commandRunner));

    /// <inheritdoc />
    public async Task<Result<ComparisonTargetSet>> ListAsync(
        string? path,
        string? query,
        string? after,
        string? targetSetToken,
        CancellationToken cancellationToken)
    {
        var startedAt = Stopwatch.GetTimestamp();
        var requestValidation = ValidateRequest(query, after, targetSetToken);
        if (requestValidation.IsFailure)
        {
            return Result.ErrorFromResult<ComparisonTargetSet>(requestValidation);
        }

        cancellationToken.ThrowIfCancellationRequested();
        var remaining = Remaining(startedAt, ComparisonLimits.TargetDiscoveryTimeout);
        if (remaining <= TimeSpan.Zero)
        {
            return TimedOut();
        }

        using var deadline = new CancellationTokenSource(remaining);
        using var actionCancellation = CancellationTokenSource.CreateLinkedTokenSource(
            cancellationToken,
            deadline.Token);

        try
        {
            var repositoryResult = await this._repositoryInspector.InspectAsync(
                path,
                remaining,
                ComparisonErrors(),
                actionCancellation.Token);
            if (repositoryResult.IsFailure)
            {
                return Result.ErrorFromResult<ComparisonTargetSet>(repositoryResult);
            }

            return await this.ListForRepositoryAsync(
                repositoryResult.Data!,
                query,
                after,
                targetSetToken,
                startedAt,
                ComparisonLimits.TargetDiscoveryTimeout,
                actionCancellation.Token);
        }
        catch (OperationCanceledException) when (
            !cancellationToken.IsCancellationRequested && deadline.IsCancellationRequested)
        {
            return TimedOut();
        }
    }

    /// <inheritdoc />
    public async Task<Result<ComparisonTargetSet>> ListForRepositoryAsync(
        RepositoryDescriptor repository,
        string? query,
        string? after,
        string? targetSetToken,
        long startedAt,
        TimeSpan totalBudget,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(repository);
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(totalBudget, TimeSpan.Zero);
        var requestValidation = ValidateRequest(query, after, targetSetToken);
        if (requestValidation.IsFailure)
        {
            return Result.ErrorFromResult<ComparisonTargetSet>(requestValidation);
        }

        cancellationToken.ThrowIfCancellationRequested();
        var remaining = Remaining(startedAt, totalBudget);
        if (remaining <= TimeSpan.Zero)
        {
            return TimedOut();
        }

        var commandResult = await this._commandRunner.RunAsync(
            new GitCommand(
                [
                    "-C",
                    repository.CanonicalPath,
                    "-c",
                    "diff.external=",
                    "-c",
                    "diff.trustExitCode=false",
                    "for-each-ref",
                    "--format=%(refname)%00%(objectname)%00%(objecttype)%00%(symref)%00%(upstream:remotename)%00",
                    "refs/heads",
                    "refs/remotes",
                ],
                remaining,
                ComparisonLimits.MaximumFactOutputBytes,
                ComparisonLimits.MaximumDiagnosticBytes,
                ComparisonErrors()),
            cancellationToken);
        if (commandResult.IsFailure)
        {
            return Result.ErrorFromResult<ComparisonTargetSet>(commandResult);
        }

        var parseResult = GitComparisonOutputParser.ParseTargetRecords(commandResult.Data!);
        if (parseResult.IsFailure)
        {
            return Result.ErrorFromResult<ComparisonTargetSet>(parseResult);
        }

        var discovered = Discover(repository, parseResult.Data!);
        var token = ComparisonFingerprint.CreateTargetSetToken(
            repository.CanonicalPath,
            query,
            discovered.Targets,
            discovered.UnsupportedTargetCount);

        if (targetSetToken is not null &&
            !TokensEqual(targetSetToken, token))
        {
            return Result.Fail<ComparisonTargetSet>(
                OperationError.Conflict(
                    "Comparison targets changed. Reload the target list.",
                    ComparisonErrorCode.TargetsChanged));
        }

        var filtered = query is null
            ? discovered.Targets
            : discovered.Targets
                .Where(target => target.Name.Contains(query, StringComparison.OrdinalIgnoreCase))
                .ToArray();

        IReadOnlyList<ComparisonTargetDescriptor> page = filtered;
        if (after is not null)
        {
            var cursorIndex = IndexOfFullName(filtered, after);
            if (cursorIndex < 0)
            {
                return InvalidPage<ComparisonTargetSet>();
            }

            page = filtered.Skip(cursorIndex + 1).ToArray();
        }

        var suggestion = query is null && after is null
            ? discovered.SuggestedTarget
            : null;
        return Result.Success(
            new ComparisonTargetSet(
                page,
                suggestion,
                token,
                discovered.UnsupportedTargetCount)
            {
                UnpagedTargets = filtered,
            });
    }

    private static Result ValidateRequest(
        string? query,
        string? after,
        string? targetSetToken)
    {
        if (query is not null &&
            (query.Contains('\0') ||
             !HasAtMostScalars(query, ComparisonLimits.MaximumQueryScalars)))
        {
            return Result.Fail(
                OperationError.Validation(
                    "The target query is invalid.",
                    ComparisonErrorCode.InvalidTargetQuery));
        }

        if ((after is null) != (targetSetToken is null) ||
            after is not null && !IsValidCursor(after) ||
            targetSetToken is not null && !IsValidToken(targetSetToken))
        {
            return InvalidPage();
        }

        return Result.Success();
    }

    private static (
        IReadOnlyList<ComparisonTargetDescriptor> Targets,
        ComparisonTargetDescriptor? SuggestedTarget,
        int UnsupportedTargetCount) Discover(
        RepositoryDescriptor repository,
        IReadOnlyList<GitComparisonTargetRecord> records)
    {
        var targets = new List<ComparisonTargetDescriptor>();
        var symbolicDefaults = new Dictionary<string, string>(StringComparer.Ordinal);
        var unsupportedTargetCount = 0;
        string? configuredRemote = null;
        var currentFullName = repository.Head is BranchRepositoryHead branch
            ? "refs/heads/" + branch.Name
            : null;

        foreach (var record in records)
        {
            if (!HasAtMostScalars(record.FullName, ComparisonLimits.MaximumRefScalars))
            {
                unsupportedTargetCount++;
                continue;
            }

            if (TryReadSymbolicRemoteDefault(record, out var remoteName))
            {
                symbolicDefaults[remoteName] = record.SymbolicTarget!;
                continue;
            }

            if (record.ObjectType != "commit" || record.SymbolicTarget is not null)
            {
                unsupportedTargetCount++;
                continue;
            }

            if (record.FullName.StartsWith("refs/heads/", StringComparison.Ordinal))
            {
                if (record.FullName == currentFullName)
                {
                    configuredRemote = record.UpstreamRemote;
                    continue;
                }

                targets.Add(
                    new ComparisonTargetDescriptor(
                        ComparisonTargetKind.Local,
                        record.FullName["refs/heads/".Length..],
                        record.FullName,
                        record.Revision));
                continue;
            }

            if (record.FullName.StartsWith("refs/remotes/", StringComparison.Ordinal) &&
                record.FullName["refs/remotes/".Length..].Contains('/'))
            {
                targets.Add(
                    new ComparisonTargetDescriptor(
                        ComparisonTargetKind.RemoteTracking,
                        record.FullName["refs/remotes/".Length..],
                        record.FullName,
                        record.Revision));
                continue;
            }

            unsupportedTargetCount++;
        }

        var orderedTargets = targets
            .OrderBy(target => target.Kind)
            .ThenBy(target => target.FullName, StringComparer.Ordinal)
            .ToArray();
        var byFullName = orderedTargets.ToDictionary(
            target => target.FullName,
            StringComparer.Ordinal);
        var suggested = ResolveSuggestedTarget(
            configuredRemote,
            symbolicDefaults,
            byFullName);
        return (orderedTargets, suggested, unsupportedTargetCount);
    }

    private static ComparisonTargetDescriptor? ResolveSuggestedTarget(
        string? configuredRemote,
        IReadOnlyDictionary<string, string> symbolicDefaults,
        IReadOnlyDictionary<string, ComparisonTargetDescriptor> targets)
    {
        if (configuredRemote is not null &&
            TryResolveDefault(configuredRemote, symbolicDefaults, targets, out var configured))
        {
            return configured;
        }

        return configuredRemote != "origin" &&
               TryResolveDefault("origin", symbolicDefaults, targets, out var origin)
            ? origin
            : null;
    }

    private static bool TryResolveDefault(
        string remoteName,
        IReadOnlyDictionary<string, string> symbolicDefaults,
        IReadOnlyDictionary<string, ComparisonTargetDescriptor> targets,
        out ComparisonTargetDescriptor? target)
    {
        target = null;
        return symbolicDefaults.TryGetValue(remoteName, out var symbolicTarget) &&
               symbolicTarget.StartsWith(
                   $"refs/remotes/{remoteName}/",
                   StringComparison.Ordinal) &&
               targets.TryGetValue(symbolicTarget, out target);
    }

    private static bool TryReadSymbolicRemoteDefault(
        GitComparisonTargetRecord record,
        out string remoteName)
    {
        remoteName = string.Empty;
        if (record.ObjectType != "commit" ||
            record.SymbolicTarget is null ||
            !record.FullName.StartsWith("refs/remotes/", StringComparison.Ordinal) ||
            !record.FullName.EndsWith("/HEAD", StringComparison.Ordinal))
        {
            return false;
        }

        var suffix = record.FullName["refs/remotes/".Length..];
        remoteName = suffix[..^"/HEAD".Length];
        return remoteName.Length > 0 && !remoteName.Contains('/');
    }

    private static int IndexOfFullName(
        IReadOnlyList<ComparisonTargetDescriptor> targets,
        string fullName)
    {
        for (var index = 0; index < targets.Count; index++)
        {
            if (targets[index].FullName == fullName)
            {
                return index;
            }
        }

        return -1;
    }

    private static bool IsValidCursor(string value)
    {
        if (!HasAtMostScalars(value, ComparisonLimits.MaximumRefScalars) ||
            !(value.StartsWith("refs/heads/", StringComparison.Ordinal) ||
              value.StartsWith("refs/remotes/", StringComparison.Ordinal)) ||
            value.EndsWith('/') ||
            value.EndsWith('.') ||
            value.Contains("..", StringComparison.Ordinal) ||
            value.Contains("//", StringComparison.Ordinal) ||
            value.Contains("@{", StringComparison.Ordinal) ||
            value.Any(
                character =>
                    char.IsControl(character) ||
                    character is ' ' or '~' or '^' or ':' or '?' or '*' or '[' or '\\'))
        {
            return false;
        }

        return value.Split('/').All(
            component =>
                component.Length > 0 &&
                !component.StartsWith('.') &&
                !component.EndsWith(".lock", StringComparison.Ordinal));
    }

    private static bool IsValidToken(string value) =>
        value.Length == ComparisonLimits.FingerprintHexLength &&
        value.All(character => character is >= '0' and <= '9' or >= 'a' and <= 'f');

    private static bool TokensEqual(
        string supplied,
        string current) =>
        CryptographicOperations.FixedTimeEquals(
            Encoding.ASCII.GetBytes(supplied),
            Encoding.ASCII.GetBytes(current));

    private static bool HasAtMostScalars(
        string value,
        int maximum)
    {
        var remaining = value.AsSpan();
        var count = 0;

        while (!remaining.IsEmpty)
        {
            var status = Rune.DecodeFromUtf16(
                remaining,
                out _,
                out var charactersConsumed);
            if (status != OperationStatus.Done || ++count > maximum)
            {
                return false;
            }

            remaining = remaining[charactersConsumed..];
        }

        return true;
    }

    private static TimeSpan Remaining(
        long startedAt,
        TimeSpan totalBudget) =>
        totalBudget - Stopwatch.GetElapsedTime(startedAt);

    private static GitCommandErrorPolicy ComparisonErrors() =>
        new(TimedOutError, TooLargeError, InspectionFailedError);

    private static Result<ComparisonTargetSet> TimedOut() =>
        Result.Fail<ComparisonTargetSet>(TimedOutError);

    private static Result InvalidPage() =>
        Result.Fail(
            OperationError.Validation(
                "The target page request is invalid.",
                ComparisonErrorCode.InvalidTargetPage));

    private static Result<T> InvalidPage<T>() =>
        Result.Fail<T>(
            OperationError.Validation(
                "The target page request is invalid.",
                ComparisonErrorCode.InvalidTargetPage));
}
