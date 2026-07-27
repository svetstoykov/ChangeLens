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
using Microsoft.Extensions.Logging;

namespace ChangeLens.Core.Comparisons.Services;

/// <summary>
///     Checks whether a prepared comparison still matches local repository facts.
/// </summary>
/// <remarks>
///     <para>
///         The Engine host registers this stateless service as a singleton. It is safe to call concurrently.
///     </para>
///     <para>
///         The check reads only the compact facts that contribute to a prepared freshness token and does not
///         replace the prepared comparison summary.
///     </para>
/// </remarks>
/// <param name="repositoryInspector">The repository inspection service. Cannot be <see langword="null" />.</param>
/// <param name="targetDiscovery">The comparison-target discovery service. Cannot be <see langword="null" />.</param>
/// <param name="commandRunner">The controlled Git process boundary. Cannot be <see langword="null" />.</param>
/// <param name="logger">The logger for freshness-check outcomes. Cannot be <see langword="null" />.</param>
/// <exception cref="ArgumentNullException">
///     <paramref name="repositoryInspector" />, <paramref name="targetDiscovery" />,
///     <paramref name="commandRunner" />, or <paramref name="logger" /> is <see langword="null" />.
/// </exception>
public sealed class GitComparisonFreshnessChecker(
    IGitRepositoryInspector repositoryInspector,
    IGitComparisonTargetDiscovery targetDiscovery,
    IGitCommandRunner commandRunner,
    ILogger<GitComparisonFreshnessChecker> logger)
    : IGitComparisonFreshnessChecker
{
    private static readonly UTF8Encoding StrictUtf8 = new(false, true);

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

    private static readonly OperationError InvalidFreshnessTokenError = OperationError.Validation(
        "The comparison freshness token is invalid.",
        ComparisonErrorCode.InvalidFreshnessToken);

    private static readonly OperationError TargetInvalidError = OperationError.UnprocessableInput(
        "The selected comparison target is not supported.",
        ComparisonErrorCode.TargetInvalid);

    private readonly IGitRepositoryInspector _repositoryInspector =
        repositoryInspector ?? throw new ArgumentNullException(nameof(repositoryInspector));

    private readonly IGitComparisonTargetDiscovery _targetDiscovery =
        targetDiscovery ?? throw new ArgumentNullException(nameof(targetDiscovery));

    private readonly IGitCommandRunner _commandRunner =
        commandRunner ?? throw new ArgumentNullException(nameof(commandRunner));

    private readonly ILogger<GitComparisonFreshnessChecker> _logger =
        logger ?? throw new ArgumentNullException(nameof(logger));

    /// <inheritdoc />
    public async Task<Result<ComparisonFreshnessState>> CheckAsync(
        string? path,
        string? target,
        string? freshnessToken,
        CancellationToken cancellationToken)
    {
        if (!IsValidToken(freshnessToken))
        {
            this._logger.LogDebug(
                "Rejected comparison freshness check for target {Target}: freshness token shape is not approved.",
                target);
            return InvalidFreshnessTokenError;
        }

        if (!IsApprovedTargetShape(target))
        {
            this._logger.LogDebug(
                "Rejected comparison freshness check for target {Target}: target shape is not approved.",
                target);
            return TargetInvalidError;
        }

        cancellationToken.ThrowIfCancellationRequested();
        var startedAt = Stopwatch.GetTimestamp();
        using var deadline = new CancellationTokenSource(ComparisonLimits.FreshnessTimeout);
        using var actionCancellation = CancellationTokenSource.CreateLinkedTokenSource(
            cancellationToken,
            deadline.Token);

        try
        {
            var repositoryResult = await this.InspectRepositoryAsync(
                path,
                startedAt,
                actionCancellation.Token);
            if (repositoryResult.IsFailure)
            {
                return Result.ErrorFromResult<ComparisonFreshnessState>(repositoryResult);
            }

            var repository = repositoryResult.Data!;
            var targetsResult = await this._targetDiscovery.ListForRepositoryAsync(
                repository,
                null,
                null,
                null,
                startedAt,
                ComparisonLimits.FreshnessTimeout,
                actionCancellation.Token);
            if (targetsResult.IsFailure)
            {
                return Result.ErrorFromResult<ComparisonFreshnessState>(targetsResult);
            }

            var selectedTarget = targetsResult.Data!.Targets.SingleOrDefault(
                candidate => StringComparer.Ordinal.Equals(candidate.FullName, target));
            if (selectedTarget is null)
            {
                return Result.Success(ComparisonFreshnessState.Stale);
            }

            var checkFormatResult = await this.RunAsync(
                repository.CanonicalPath,
                startedAt,
                ["check-ref-format", target!],
                actionCancellation.Token);
            if (checkFormatResult.IsFailure)
            {
                return Result.ErrorFromResult<ComparisonFreshnessState>(checkFormatResult);
            }

            var checkFormatOutput = checkFormatResult.Data!;
            if (IsQuietInvalidTarget(checkFormatOutput))
            {
                return Result.Success(ComparisonFreshnessState.Stale);
            }

            if (!HasQuietEmptyOutput(checkFormatOutput))
            {
                return InspectionFailedError;
            }

            var targetRevisionResult = await this.RunAsync(
                repository.CanonicalPath,
                startedAt,
                ["rev-parse", "--verify", target + "^{commit}"],
                actionCancellation.Token);
            if (targetRevisionResult.IsFailure)
            {
                return Result.ErrorFromResult<ComparisonFreshnessState>(targetRevisionResult);
            }

            if (IsConfirmedMissingTarget(targetRevisionResult.Data!))
            {
                return Result.Success(ComparisonFreshnessState.Stale);
            }

            var parsedTargetRevision = ParseSingleRevision(targetRevisionResult.Data!);
            if (parsedTargetRevision.IsFailure)
            {
                return Result.ErrorFromResult<ComparisonFreshnessState>(parsedTargetRevision);
            }

            var statusResult = await this.RunAsync(
                repository.CanonicalPath,
                startedAt,
                StatusArguments(),
                actionCancellation.Token);
            if (statusResult.IsFailure)
            {
                return Result.ErrorFromResult<ComparisonFreshnessState>(statusResult);
            }

            var parsedStatus = GitComparisonOutputParser.ParseWorkingTree(statusResult.Data!);
            if (parsedStatus.IsFailure)
            {
                return Result.ErrorFromResult<ComparisonFreshnessState>(parsedStatus);
            }

            var resolvedTarget = selectedTarget with { Revision = parsedTargetRevision.Data! };
            var currentToken = ComparisonFingerprint.CreateFreshnessToken(
                repository,
                resolvedTarget,
                targetsResult.Data.TargetSetToken,
                parsedStatus.Data!);
            var state = TokensEqual(freshnessToken!, currentToken)
                ? ComparisonFreshnessState.Current
                : ComparisonFreshnessState.Stale;
            this._logger.LogInformation(
                "Comparison freshness check for target {Target} resolved to {FreshnessState} in " +
                "{ElapsedMilliseconds:0.000} ms.",
                target,
                state,
                Stopwatch.GetElapsedTime(startedAt).TotalMilliseconds);
            return Result.Success(state);
        }
        catch (OperationCanceledException) when (
            !cancellationToken.IsCancellationRequested && deadline.IsCancellationRequested)
        {
            this._logger.LogWarning(
                "Comparison freshness check for target {Target} timed out after {ElapsedMilliseconds:0.000} ms.",
                target,
                Stopwatch.GetElapsedTime(startedAt).TotalMilliseconds);
            return TimedOutError;
        }
    }

    /// <summary>
    ///     Inspects the selected repository with the time remaining in the freshness action.
    /// </summary>
    /// <param name="path">The selected repository directory path.</param>
    /// <param name="startedAt">The monotonic timestamp at which freshness checking began.</param>
    /// <param name="cancellationToken">
    ///     A <see cref="CancellationToken" /> to observe while waiting for the task to complete.
    /// </param>
    /// <returns>
    ///     A task that represents the asynchronous operation. The task result contains the repository descriptor.
    /// </returns>
    private async Task<Result<RepositoryDescriptor>> InspectRepositoryAsync(
        string? path,
        long startedAt,
        CancellationToken cancellationToken)
    {
        var remaining = Remaining(startedAt);
        return remaining <= TimeSpan.Zero
            ? TimedOutError
            : await this._repositoryInspector.InspectAsync(
                path,
                remaining,
                ComparisonErrors(),
                cancellationToken);
    }

    /// <summary>
    ///     Runs one fixed read-only comparison command with the action's remaining time and stream bounds.
    /// </summary>
    /// <param name="canonicalPath">The canonical repository root. Cannot be <see langword="null" />.</param>
    /// <param name="startedAt">The monotonic timestamp at which freshness checking began.</param>
    /// <param name="subcommandArguments">
    ///     The fixed Git subcommand arguments. Cannot be <see langword="null" />.
    /// </param>
    /// <param name="cancellationToken">
    ///     A <see cref="CancellationToken" /> to observe while waiting for the task to complete.
    /// </param>
    /// <returns>
    ///     A task that represents the asynchronous operation. The task result contains bounded Git output.
    /// </returns>
    private Task<Result<GitCommandOutput>> RunAsync(
        string canonicalPath,
        long startedAt,
        IReadOnlyList<string> subcommandArguments,
        CancellationToken cancellationToken)
    {
        var remaining = Remaining(startedAt);
        if (remaining <= TimeSpan.Zero)
        {
            return Task.FromResult<Result<GitCommandOutput>>(TimedOutError);
        }

        return this._commandRunner.RunAsync(
            new GitCommand(
                DirectArguments(canonicalPath, subcommandArguments),
                remaining,
                ComparisonLimits.MaximumFactOutputBytes,
                ComparisonLimits.MaximumDiagnosticBytes,
                ComparisonErrors()),
            cancellationToken);
    }

    /// <summary>
    ///     Prepends the canonical root and fixed safety configuration to a Git subcommand.
    /// </summary>
    /// <param name="canonicalPath">The canonical repository root. Cannot be <see langword="null" />.</param>
    /// <param name="subcommandArguments">
    ///     The fixed Git subcommand arguments. Cannot be <see langword="null" />.
    /// </param>
    /// <returns>The complete separate-argument Git invocation.</returns>
    private static IReadOnlyList<string> DirectArguments(
        string canonicalPath,
        IReadOnlyList<string> subcommandArguments) =>
        [
            "-C",
            canonicalPath,
            "-c",
            "core.fsmonitor=false",
            "-c",
            "diff.external=",
            "-c",
            "diff.trustExitCode=false",
            "-c",
            "diff.renames=true",
            .. subcommandArguments,
        ];

    /// <summary>
    ///     Creates the fixed porcelain-v2 status argument sequence used for freshness facts.
    /// </summary>
    /// <returns>The status subcommand arguments.</returns>
    private static IReadOnlyList<string> StatusArguments() =>
        [
            "status",
            "--porcelain=v2",
            "-z",
            "--untracked-files=all",
            "--ignore-submodules=none",
            "--find-renames=50%",
        ];

    /// <summary>
    ///     Requires exactly one full revision from a successful quiet Git command.
    /// </summary>
    /// <param name="output">The captured Git output. Cannot be <see langword="null" />.</param>
    /// <returns>The parsed revision or the stable comparison inspection failure.</returns>
    private static Result<string> ParseSingleRevision(GitCommandOutput output)
    {
        var parsed = GitComparisonOutputParser.ParseMergeBases(output);
        return parsed.IsSuccess && parsed.Data!.Count == 1
            ? Result.Success<string>(parsed.Data[0])
            : parsed.IsFailure
                ? Result.ErrorFromResult<string>(parsed)
                : InspectionFailedError;
    }

    /// <summary>
    ///     Validates the supplied lowercase hexadecimal SHA-256 freshness token.
    /// </summary>
    /// <param name="token">The supplied token.</param>
    /// <returns><see langword="true" /> when the token has the exact approved shape.</returns>
    private static bool IsValidToken(string? token) =>
        token is not null &&
        token.Length == ComparisonLimits.FingerprintHexLength &&
        token.All(character => character is >= '0' and <= '9' or >= 'a' and <= 'f');

    /// <summary>
    ///     Validates the target namespace, ref shape, and Unicode bound before repository I/O.
    /// </summary>
    /// <param name="target">The supplied exact target.</param>
    /// <returns><see langword="true" /> when the target can proceed to exact discovery and Git validation.</returns>
    private static bool IsApprovedTargetShape(string? target)
    {
        if (string.IsNullOrWhiteSpace(target) ||
            target.Contains('\0') ||
            !HasAtMostScalars(target, ComparisonLimits.MaximumRefScalars) ||
            target.EndsWith('/') ||
            target.EndsWith('.') ||
            target.Contains("..", StringComparison.Ordinal) ||
            target.Contains("//", StringComparison.Ordinal) ||
            target.Contains("@{", StringComparison.Ordinal) ||
            target.Any(
                character =>
                    char.IsControl(character) ||
                    character is ' ' or '~' or '^' or ':' or '?' or '*' or '[' or '\\') ||
            target.Split('/').Any(
                component =>
                    component.Length == 0 ||
                    component.StartsWith('.') ||
                    component.EndsWith(".lock", StringComparison.Ordinal)))
        {
            return false;
        }

        if (target.StartsWith("refs/heads/", StringComparison.Ordinal))
        {
            return target.Length > "refs/heads/".Length;
        }

        if (!target.StartsWith("refs/remotes/", StringComparison.Ordinal))
        {
            return false;
        }

        var suffix = target["refs/remotes/".Length..];
        return suffix.Contains('/') &&
               !suffix.StartsWith('/') &&
               !suffix.EndsWith('/');
    }

    /// <summary>
    ///     Validates Unicode scalar encoding and a maximum scalar count.
    /// </summary>
    /// <param name="value">The value to inspect. Cannot be <see langword="null" />.</param>
    /// <param name="maximum">The inclusive maximum number of Unicode scalars.</param>
    /// <returns><see langword="true" /> when the value is valid and within the bound.</returns>
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

    /// <summary>
    ///     Recognizes only the reviewed C-locale diagnostic for an exact target that no longer exists.
    /// </summary>
    /// <param name="output">The captured target-resolution output. Cannot be <see langword="null" />.</param>
    /// <returns><see langword="true" /> only for the bounded, exact missing-revision outcome.</returns>
    private static bool IsConfirmedMissingTarget(GitCommandOutput output)
    {
        if (output.ExitCode != 128 ||
            output.StandardOutput.Length != 0 ||
            !HasBoundedValidDiagnostics(output.StandardError))
        {
            return false;
        }

        return RemoveOneTerminalLineEnding(output.StandardError).Equals(
            "fatal: Needed a single revision",
            StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    ///     Determines whether a command succeeded without producing either stream.
    /// </summary>
    /// <param name="output">The captured Git output. Cannot be <see langword="null" />.</param>
    /// <returns><see langword="true" /> for exit code zero and two empty streams.</returns>
    private static bool HasQuietEmptyOutput(GitCommandOutput output) =>
        output.ExitCode == 0 &&
        output.StandardOutput.Length == 0 &&
        output.StandardError.Length == 0;

    /// <summary>
    ///     Recognizes a quiet invalid-reference outcome for a target that changed after preparation.
    /// </summary>
    /// <param name="output">The captured ref-format output. Cannot be <see langword="null" />.</param>
    /// <returns><see langword="true" /> for exit code one and two empty streams.</returns>
    private static bool IsQuietInvalidTarget(GitCommandOutput output) =>
        output.ExitCode == 1 &&
        output.StandardOutput.Length == 0 &&
        output.StandardError.Length == 0;

    /// <summary>
    ///     Validates decoded diagnostic text against UTF-8 and the diagnostic bound.
    /// </summary>
    /// <param name="value">The decoded diagnostic text. Cannot be <see langword="null" />.</param>
    /// <returns><see langword="true" /> when the text is valid and bounded.</returns>
    private static bool HasBoundedValidDiagnostics(string value)
    {
        try
        {
            return StrictUtf8.GetByteCount(value) <= ComparisonLimits.MaximumDiagnosticBytes;
        }
        catch (EncoderFallbackException)
        {
            return false;
        }
    }

    /// <summary>
    ///     Removes one optional LF or CRLF while preserving every other character.
    /// </summary>
    /// <param name="value">The captured text. Cannot be <see langword="null" />.</param>
    /// <returns>The text without one optional terminal line ending.</returns>
    private static string RemoveOneTerminalLineEnding(string value) =>
        value.EndsWith("\r\n", StringComparison.Ordinal)
            ? value[..^2]
            : value.EndsWith('\n')
                ? value[..^1]
                : value;

    /// <summary>
    ///     Compares two approved freshness tokens without an early exit.
    /// </summary>
    /// <param name="supplied">The caller-supplied freshness token. Cannot be <see langword="null" />.</param>
    /// <param name="current">The freshly computed token. Cannot be <see langword="null" />.</param>
    /// <returns><see langword="true" /> when both tokens are identical.</returns>
    private static bool TokensEqual(
        string supplied,
        string current) =>
        CryptographicOperations.FixedTimeEquals(
            Encoding.ASCII.GetBytes(supplied),
            Encoding.ASCII.GetBytes(current));

    /// <summary>
    ///     Calculates the time remaining in the single freshness budget.
    /// </summary>
    /// <param name="startedAt">The monotonic timestamp at which freshness checking began.</param>
    /// <returns>The remaining duration, which can be nonpositive.</returns>
    private static TimeSpan Remaining(long startedAt) =>
        ComparisonLimits.FreshnessTimeout - Stopwatch.GetElapsedTime(startedAt);

    /// <summary>
    ///     Creates the immutable terminal-error policy for one comparison command.
    /// </summary>
    /// <returns>The comparison timeout, output-limit, and inspection errors.</returns>
    private static GitCommandErrorPolicy ComparisonErrors() =>
        new(TimedOutError, TooLargeError, InspectionFailedError);
}
