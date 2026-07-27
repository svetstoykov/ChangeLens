using System.Diagnostics;
using ChangeLens.Core.Comparisons.Constants;
using ChangeLens.Core.Git.Constants;
using ChangeLens.Core.Git.Interfaces;
using ChangeLens.Core.Git.Models;
using ChangeLens.Core.Git.Parsers;
using ChangeLens.Core.Repositories.Models;
using ChangeLens.Core.Results.Models;
using Microsoft.Extensions.Logging;

namespace ChangeLens.Core.Git.Services;

/// <summary>
///     Detects and refreshes a cached remote-tracking comparison baseline against the server.
/// </summary>
/// <remarks>
///     <para>
///         The Engine host registers this stateless service as a singleton. It is safe to call concurrently.
///     </para>
///     <para>
///         Detection uses <c>git ls-remote --heads</c>, which asks the server for a single ref and transfers no
///         objects. Refresh uses <c>git fetch</c> scoped to exactly one ref, updating only the corresponding
///         remote-tracking reference. Neither operation touches local branches, the working tree, the index, or
///         <c>HEAD</c>.
///     </para>
/// </remarks>
/// <param name="repositoryInspector">The repository inspection service. Cannot be <see langword="null" />.</param>
/// <param name="commandRunner">The controlled Git process boundary. Cannot be <see langword="null" />.</param>
/// <param name="logger">The logger for remote baseline check and refresh outcomes. Cannot be <see langword="null" />.</param>
public sealed class GitRemoteBaselineTracker(
    IGitRepositoryInspector repositoryInspector,
    IGitCommandRunner commandRunner,
    ILogger<GitRemoteBaselineTracker> logger) : IGitRemoteBaselineTracker
{
    private static readonly OperationError TargetInvalidError = OperationError.UnprocessableInput(
        "The selected comparison target is not supported.",
        ComparisonErrorCode.TargetInvalid);

    private static readonly OperationError NoRemoteConfiguredError = OperationError.UnprocessableInput(
        "The repository has no configured remote for the selected target.",
        ComparisonErrorCode.NoRemoteConfigured);

    private static readonly OperationError InspectionFailedError =
        OperationError.ExternalDependencyFailure(
            "Git comparison inspection failed.",
            ComparisonErrorCode.InspectionFailed);

    private readonly IGitRepositoryInspector _repositoryInspector =
        repositoryInspector ?? throw new ArgumentNullException(nameof(repositoryInspector));

    private readonly IGitCommandRunner _commandRunner =
        commandRunner ?? throw new ArgumentNullException(nameof(commandRunner));

    private readonly ILogger<GitRemoteBaselineTracker> _logger =
        logger ?? throw new ArgumentNullException(nameof(logger));

    /// <inheritdoc />
    public async Task<Result<RemoteBaselineCheckResult>> CheckAsync(
        string? path,
        string? target,
        CancellationToken cancellationToken)
    {
        if (!IsApprovedTarget(target))
        {
            this._logger.LogDebug(
                "Rejected remote baseline check for target {Target}: target shape is not approved.",
                target);
            return TargetInvalidError;
        }

        cancellationToken.ThrowIfCancellationRequested();
        var startedAt = Stopwatch.GetTimestamp();
        using var deadline = new CancellationTokenSource(GitRemoteConstants.CheckTimeout);
        using var actionCancellation = CancellationTokenSource.CreateLinkedTokenSource(
            cancellationToken,
            deadline.Token);

        try
        {
            var repositoryResult = await this.InspectRepositoryAsync(
                path,
                startedAt,
                GitRemoteConstants.CheckTimeout,
                actionCancellation.Token);
            if (repositoryResult.IsFailure)
            {
                return Result.ErrorFromResult<RemoteBaselineCheckResult>(repositoryResult);
            }

            var repository = repositoryResult.Data!;
            var remoteNamesResult = await this.ListRemoteNamesAsync(
                repository.CanonicalPath,
                startedAt,
                GitRemoteConstants.CheckTimeout,
                actionCancellation.Token);
            if (remoteNamesResult.IsFailure)
            {
                return Result.ErrorFromResult<RemoteBaselineCheckResult>(remoteNamesResult);
            }

            var remoteNames = remoteNamesResult.Data!;
            if (remoteNames.Count == 0)
            {
                return Result.Success(new RemoteBaselineCheckResult(RemoteBaselineState.NoRemote, null));
            }

            var resolution = GitOutputParser.ResolveRemoteBranch(target!, remoteNames);
            if (resolution is null)
            {
                this._logger.LogDebug(
                    "Rejected remote baseline check for target {Target}: target does not match a known remote.",
                    target);
                return TargetInvalidError;
            }

            var (remote, branch) = resolution.Value;
            var localRevisionResult = await this.RunAsync(
                repository.CanonicalPath,
                startedAt,
                GitRemoteConstants.CheckTimeout,
                ["rev-parse", "--verify", target + "^{commit}"],
                actionCancellation.Token);
            if (localRevisionResult.IsFailure)
            {
                return Result.ErrorFromResult<RemoteBaselineCheckResult>(localRevisionResult);
            }

            if (IsConfirmedMissingTarget(localRevisionResult.Data!))
            {
                this._logger.LogWarning(
                    "Remote baseline check for target {Target} found no local commit; the target may have been " +
                    "removed since it was last discovered.",
                    target);
                return TargetInvalidError;
            }

            if (localRevisionResult.Data!.ExitCode != 0)
            {
                return InspectionFailedError;
            }

            var localRevision = GitOutputParser.ParseRevision(localRevisionResult.Data!);
            if (localRevision.IsFailure)
            {
                return Result.ErrorFromResult<RemoteBaselineCheckResult>(localRevision);
            }

            var expectedRef = "refs/heads/" + branch;
            var lsRemoteResult = await this.RunAsync(
                repository.CanonicalPath,
                startedAt,
                GitRemoteConstants.CheckTimeout,
                ["ls-remote", "--heads", remote, branch],
                actionCancellation.Token);
            if (lsRemoteResult.IsFailure)
            {
                return Result.ErrorFromResult<RemoteBaselineCheckResult>(lsRemoteResult);
            }

            var lsRemoteOutput = lsRemoteResult.Data!;
            if (lsRemoteOutput.ExitCode == 0 &&
                lsRemoteOutput.StandardError.Length == 0 &&
                lsRemoteOutput.StandardOutput.Length == 0)
            {
                this._logger.LogWarning(
                    "Remote baseline check for target {Target} against remote {Remote} found no matching " +
                    "remote branch.",
                    target,
                    remote);
                return TargetInvalidError;
            }

            if (lsRemoteOutput.ExitCode != 0)
            {
                this._logger.LogWarning(
                    "Remote baseline check for target {Target} against remote {Remote} failed with exit code " +
                    "{ExitCode} in {ElapsedMilliseconds:0.000} ms.",
                    target,
                    remote,
                    lsRemoteOutput.ExitCode,
                    Stopwatch.GetElapsedTime(startedAt).TotalMilliseconds);
                return 
                    GitDiagnosticClassifier.ClassifyRemoteFailure(lsRemoteOutput);
            }

            var remoteRevisionResult = GitOutputParser.ParseLsRemoteRevision(lsRemoteOutput, expectedRef);
            if (remoteRevisionResult.IsFailure)
            {
                return Result.ErrorFromResult<RemoteBaselineCheckResult>(remoteRevisionResult);
            }

            var remoteRevision = remoteRevisionResult.Data!;
            var state = string.Equals(remoteRevision, localRevision.Data!, StringComparison.Ordinal)
                ? RemoteBaselineState.Current
                : RemoteBaselineState.Moved;
            this._logger.LogInformation(
                "Remote baseline check for target {Target} against remote {Remote} resolved to {BaselineState} " +
                "in {ElapsedMilliseconds:0.000} ms.",
                target,
                remote,
                state,
                Stopwatch.GetElapsedTime(startedAt).TotalMilliseconds);
            return Result.Success(new RemoteBaselineCheckResult(state, remoteRevision));
        }
        catch (OperationCanceledException) when (
            !cancellationToken.IsCancellationRequested && deadline.IsCancellationRequested)
        {
            this._logger.LogWarning(
                "Remote baseline check for target {Target} timed out after {ElapsedMilliseconds:0.000} ms.",
                target,
                Stopwatch.GetElapsedTime(startedAt).TotalMilliseconds);
            return TimedOutError();
        }
    }

    /// <inheritdoc />
    public async Task<Result<string>> RefreshAsync(
        string? path,
        string? target,
        CancellationToken cancellationToken)
    {
        if (!IsApprovedTarget(target))
        {
            this._logger.LogDebug(
                "Rejected remote baseline refresh for target {Target}: target shape is not approved.",
                target);
            return TargetInvalidError;
        }

        cancellationToken.ThrowIfCancellationRequested();
        var startedAt = Stopwatch.GetTimestamp();
        using var deadline = new CancellationTokenSource(GitRemoteConstants.RefreshTimeout);
        using var actionCancellation = CancellationTokenSource.CreateLinkedTokenSource(
            cancellationToken,
            deadline.Token);

        try
        {
            var repositoryResult = await this.InspectRepositoryAsync(
                path,
                startedAt,
                GitRemoteConstants.RefreshTimeout,
                actionCancellation.Token);
            if (repositoryResult.IsFailure)
            {
                return Result.ErrorFromResult<string>(repositoryResult);
            }

            var repository = repositoryResult.Data!;
            var remoteNamesResult = await this.ListRemoteNamesAsync(
                repository.CanonicalPath,
                startedAt,
                GitRemoteConstants.RefreshTimeout,
                actionCancellation.Token);
            if (remoteNamesResult.IsFailure)
            {
                return Result.ErrorFromResult<string>(remoteNamesResult);
            }

            var remoteNames = remoteNamesResult.Data!;
            if (remoteNames.Count == 0)
            {
                this._logger.LogWarning(
                    "Remote baseline refresh for target {Target} found no configured remotes.",
                    target);
                return NoRemoteConfiguredError;
            }

            var resolution = GitOutputParser.ResolveRemoteBranch(target!, remoteNames);
            if (resolution is null)
            {
                this._logger.LogDebug(
                    "Rejected remote baseline refresh for target {Target}: target does not match a known remote.",
                    target);
                return TargetInvalidError;
            }

            var (remote, branch) = resolution.Value;
            var remoteTrackingRef = "refs/remotes/" + remote + "/" + branch;
            var fetchResult = await this.RunAsync(
                repository.CanonicalPath,
                startedAt,
                GitRemoteConstants.RefreshTimeout,
                ["fetch", remote, $"refs/heads/{branch}:{remoteTrackingRef}"],
                actionCancellation.Token);
            if (fetchResult.IsFailure)
            {
                return Result.ErrorFromResult<string>(fetchResult);
            }

            var fetchOutput = fetchResult.Data!;
            if (fetchOutput.ExitCode != 0)
            {
                this._logger.LogWarning(
                    "Remote baseline refresh for target {Target} against remote {Remote} failed with exit code " +
                    "{ExitCode} in {ElapsedMilliseconds:0.000} ms.",
                    target,
                    remote,
                    fetchOutput.ExitCode,
                    Stopwatch.GetElapsedTime(startedAt).TotalMilliseconds);
                return GitDiagnosticClassifier.ClassifyRemoteFailure(fetchOutput);
            }

            var revisionResult = await this.RunAsync(
                repository.CanonicalPath,
                startedAt,
                GitRemoteConstants.RefreshTimeout,
                ["rev-parse", "--verify", remoteTrackingRef + "^{commit}"],
                actionCancellation.Token);
            if (revisionResult.IsFailure)
            {
                return Result.ErrorFromResult<string>(revisionResult);
            }

            var revisionOutput = revisionResult.Data!;
            if (revisionOutput.ExitCode != 0)
            {
                return InspectionFailedError;
            }

            var parsedRevision = GitOutputParser.ParseRevision(revisionOutput);
            if (parsedRevision.IsSuccess)
            {
                this._logger.LogInformation(
                    "Refreshed remote baseline for target {Target} against remote {Remote} to revision " +
                    "{Revision} in {ElapsedMilliseconds:0.000} ms.",
                    target,
                    remote,
                    parsedRevision.Data,
                    Stopwatch.GetElapsedTime(startedAt).TotalMilliseconds);
            }

            return parsedRevision;
        }
        catch (OperationCanceledException) when (
            !cancellationToken.IsCancellationRequested && deadline.IsCancellationRequested)
        {
            this._logger.LogWarning(
                "Remote baseline refresh for target {Target} timed out after {ElapsedMilliseconds:0.000} ms.",
                target,
                Stopwatch.GetElapsedTime(startedAt).TotalMilliseconds);
            return TimedOutError();
        }
    }

    /// <summary>
    ///     Inspects the selected repository with the time remaining in the owning action.
    /// </summary>
    /// <param name="path">The selected repository directory path.</param>
    /// <param name="startedAt">The monotonic timestamp at which the owning action began.</param>
    /// <param name="totalBudget">The total time allowed for the owning action.</param>
    /// <param name="cancellationToken">
    ///     A <see cref="CancellationToken" /> to observe while waiting for the task to complete.
    /// </param>
    /// <returns>
    ///     A task that represents the asynchronous operation. The task result contains the repository descriptor.
    /// </returns>
    private async Task<Result<RepositoryDescriptor>> InspectRepositoryAsync(
        string? path,
        long startedAt,
        TimeSpan totalBudget,
        CancellationToken cancellationToken)
    {
        var remaining = Remaining(startedAt, totalBudget);
        return remaining <= TimeSpan.Zero
            ? TimedOutError()
            : await this._repositoryInspector.InspectAsync(
                path,
                remaining,
                ErrorPolicy(),
                cancellationToken);
    }

    /// <summary>
    ///     Lists the repository's configured remote names with the time remaining in the owning action.
    /// </summary>
    /// <param name="canonicalPath">The canonical repository root. Cannot be <see langword="null" />.</param>
    /// <param name="startedAt">The monotonic timestamp at which the owning action began.</param>
    /// <param name="totalBudget">The total time allowed for the owning action.</param>
    /// <param name="cancellationToken">
    ///     A <see cref="CancellationToken" /> to observe while waiting for the task to complete.
    /// </param>
    /// <returns>
    ///     A task that represents the asynchronous operation. The task result contains the configured remote names.
    /// </returns>
    private async Task<Result<IReadOnlyList<string>>> ListRemoteNamesAsync(
        string canonicalPath,
        long startedAt,
        TimeSpan totalBudget,
        CancellationToken cancellationToken)
    {
        var remoteListResult = await this.RunAsync(
            canonicalPath,
            startedAt,
            totalBudget,
            ["remote"],
            cancellationToken);
        if (remoteListResult.IsFailure)
        {
            return Result.ErrorFromResult<IReadOnlyList<string>>(remoteListResult);
        }

        return GitOutputParser.ParseRemoteNames(remoteListResult.Data!);
    }

    /// <summary>
    ///     Runs one fixed Git subcommand with the time remaining in the owning action's shared budget.
    /// </summary>
    /// <param name="canonicalPath">The canonical repository root. Cannot be <see langword="null" />.</param>
    /// <param name="startedAt">The monotonic timestamp at which the owning action began.</param>
    /// <param name="totalBudget">The total time allowed for the owning action.</param>
    /// <param name="subcommandArguments">The fixed Git subcommand arguments. Cannot be <see langword="null" />.</param>
    /// <param name="cancellationToken">
    ///     A <see cref="CancellationToken" /> to observe while waiting for the task to complete.
    /// </param>
    /// <returns>
    ///     A task that represents the asynchronous operation. The task result contains the bounded Git output.
    /// </returns>
    private Task<Result<GitCommandOutput>> RunAsync(
        string canonicalPath,
        long startedAt,
        TimeSpan totalBudget,
        IReadOnlyList<string> subcommandArguments,
        CancellationToken cancellationToken)
    {
        var remaining = Remaining(startedAt, totalBudget);
        if (remaining <= TimeSpan.Zero)
        {
            return Task.FromResult<Result<GitCommandOutput>>(TimedOutError());
        }

        IReadOnlyList<string> arguments = ["-C", canonicalPath, .. subcommandArguments];
        return this._commandRunner.RunAsync(
            new GitCommand(
                arguments,
                remaining,
                GitRemoteConstants.MaximumStreamBytes,
                GitRemoteConstants.MaximumStreamBytes,
                ErrorPolicy()),
            cancellationToken);
    }

    /// <summary>
    ///     Calculates the time remaining in the owning action's shared budget.
    /// </summary>
    /// <param name="startedAt">The monotonic timestamp at which the owning action began.</param>
    /// <param name="totalBudget">The total time allowed for the owning action.</param>
    /// <returns>The remaining duration, which can be nonpositive.</returns>
    private static TimeSpan Remaining(long startedAt, TimeSpan totalBudget) =>
        totalBudget - Stopwatch.GetElapsedTime(startedAt);

    /// <summary>
    ///     Validates the remote-tracking target namespace, ref shape, and Unicode bound before repository I/O.
    /// </summary>
    /// <param name="target">The supplied exact target.</param>
    /// <returns><see langword="true" /> when the target can proceed to remote resolution.</returns>
    private static bool IsApprovedTarget(string? target)
    {
        if (string.IsNullOrWhiteSpace(target) ||
            target.Contains('\0') ||
            target.Length > 4_096 ||
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

        if (!target.StartsWith("refs/remotes/", StringComparison.Ordinal))
        {
            return false;
        }

        var suffix = target["refs/remotes/".Length..];
        return suffix.Contains('/') && !suffix.StartsWith('/') && !suffix.EndsWith('/');
    }

    /// <summary>
    ///     Recognizes only the reviewed C-locale diagnostic for a target that no longer exists locally.
    /// </summary>
    /// <param name="output">The captured target-resolution output. Cannot be <see langword="null" />.</param>
    /// <returns><see langword="true" /> only for the bounded, exact missing-revision outcome.</returns>
    private static bool IsConfirmedMissingTarget(GitCommandOutput output)
    {
        if (output.ExitCode != 128 || output.StandardOutput.Length != 0)
        {
            return false;
        }

        var diagnostic = output.StandardError.EndsWith("\r\n", StringComparison.Ordinal)
            ? output.StandardError[..^2]
            : output.StandardError.EndsWith('\n')
                ? output.StandardError[..^1]
                : output.StandardError;

        return diagnostic.Equals("fatal: Needed a single revision", StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    ///     Creates the immutable terminal-error policy shared by every remote baseline command.
    /// </summary>
    /// <returns>The remote timeout, output-limit, and inspection errors.</returns>
    private static GitCommandErrorPolicy ErrorPolicy() =>
        new(
            TimedOutError(),
            OperationError.ExternalDependencyFailure(
                "The remote returned more data than expected.",
                ComparisonErrorCode.RemoteUnreachable),
            OperationError.ExternalDependencyFailure(
                "The remote could not be reached.",
                ComparisonErrorCode.RemoteUnreachable));

    /// <summary>
    ///     Creates the stable remote-timeout failure.
    /// </summary>
    /// <returns>The stable remote-timeout operation error.</returns>
    private static OperationError TimedOutError() =>
        OperationError.Timeout(
            "Checking the remote branch exceeded its allowed time.",
            ComparisonErrorCode.RemoteTimedOut);

}
