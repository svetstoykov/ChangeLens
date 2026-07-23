using System.Buffers;
using System.Diagnostics;
using System.Text;
using ChangeLens.Core.Git.Constants;
using ChangeLens.Core.Git.Interfaces;
using ChangeLens.Core.Git.Models;
using ChangeLens.Core.Git.Parsers;
using ChangeLens.Core.Repositories.Constants;
using ChangeLens.Core.Repositories.Models;
using ChangeLens.Core.Results.Models;

namespace ChangeLens.Core.Git.Services;

/// <summary>
///     Inspects the canonical identity and committed HEAD state of a Git repository.
/// </summary>
/// <remarks>
///     <para>
///         The Engine host registers this stateless service as a singleton. It is safe to call concurrently.
///     </para>
///     <para>
///         Each inspection uses a single 15-second budget across path resolution and the approved read-only Git facts.
///     </para>
/// </remarks>
/// <param name="commandRunner">The controlled Git process boundary. Cannot be <see langword="null" />.</param>
/// <param name="pathResolver">The physical path canonicalization boundary. Cannot be <see langword="null" />.</param>
public sealed class GitRepositoryInspector(
    IGitCommandRunner commandRunner,
    IRepositoryPathResolver pathResolver)
{
    /// <summary>
    ///     Asynchronously inspects the repository selected by the given directory path.
    /// </summary>
    /// <param name="path">
    ///     The selected repository directory path, or <see langword="null" /> when none was supplied. Invalid values return
    ///     a validation error with code <c>repository.invalidPath</c>.
    /// </param>
    /// <param name="cancellationToken">
    ///     A <see cref="CancellationToken" /> to observe while waiting for the task to complete.
    /// </param>
    /// <returns>
    ///     A task that represents the asynchronous operation. The task result contains the canonical repository identity
    ///     and committed HEAD state on success.
    /// </returns>
    /// <exception cref="OperationCanceledException">
    ///     The <paramref name="cancellationToken" /> is canceled.
    /// </exception>
    public async Task<Result<RepositoryDescriptor>> InspectAsync(
        string? path,
        CancellationToken cancellationToken)
    {
        var validationResult = ValidatePath(path);
        if (validationResult.IsFailure)
        {
            return Result.ErrorFromResult<RepositoryDescriptor>(validationResult);
        }

        using var deadline = new CancellationTokenSource(GitInspectionConstants.InspectionTimeout);
        using var inspectionCancellation = CancellationTokenSource.CreateLinkedTokenSource(
            cancellationToken,
            deadline.Token);
        var startedAt = Stopwatch.GetTimestamp();

        try
        {
            var selectedPathResult = await pathResolver.ResolveAsync(
                path!,
                inspectionCancellation.Token);
            if (selectedPathResult.IsFailure)
            {
                return Result.ErrorFromResult<RepositoryDescriptor>(selectedPathResult);
            }

            return await this.InspectResolvedAsync(
                selectedPathResult.Data!,
                startedAt,
                inspectionCancellation.Token);
        }
        catch (OperationCanceledException) when (
            !cancellationToken.IsCancellationRequested && deadline.IsCancellationRequested)
        {
            return TimedOut<RepositoryDescriptor>();
        }
    }

    /// <summary>
    ///     Executes the approved Git facts in their fixed order for a canonical selection.
    /// </summary>
    /// <param name="selectedPath">The canonical selected directory. Cannot be <see langword="null" />.</param>
    /// <param name="startedAt">The monotonic timestamp at which the shared inspection budget began.</param>
    /// <param name="cancellationToken">
    ///     A <see cref="CancellationToken" /> to observe while waiting for the task to complete.
    /// </param>
    /// <returns>
    ///     A task that represents the asynchronous operation. The task result contains the canonical repository descriptor
    ///     on success.
    /// </returns>
    private async Task<Result<RepositoryDescriptor>> InspectResolvedAsync(
        string selectedPath,
        long startedAt,
        CancellationToken cancellationToken)
    {
        var versionResult = await RunAsync(startedAt, ["--version"], cancellationToken);
        if (versionResult.IsFailure)
        {
            return Result.ErrorFromResult<RepositoryDescriptor>(versionResult);
        }

        var versionParseResult = GitOutputParser.ParseVersion(versionResult.Data!);
        if (versionParseResult.IsFailure)
        {
            return Result.ErrorFromResult<RepositoryDescriptor>(versionParseResult);
        }

        var insideResult = await RunAsync(
            startedAt,
            ["-C", selectedPath, "rev-parse", "--is-inside-work-tree"],
            cancellationToken);
        if (insideResult.IsFailure)
        {
            return Result.ErrorFromResult<RepositoryDescriptor>(insideResult);
        }

        if (insideResult.Data!.ExitCode != 0)
        {
            return Result.Fail<RepositoryDescriptor>(
                GitDiagnosticClassifier.ClassifyWorkTreeFailure(insideResult.Data));
        }

        var insideParseResult = GitOutputParser.ParseBoolean(insideResult.Data);
        if (insideParseResult.IsFailure)
        {
            return Result.ErrorFromResult<RepositoryDescriptor>(insideParseResult);
        }

        var bareResult = await RunAsync(
            startedAt,
            ["-C", selectedPath, "rev-parse", "--is-bare-repository"],
            cancellationToken);
        if (bareResult.IsFailure)
        {
            return Result.ErrorFromResult<RepositoryDescriptor>(bareResult);
        }

        if (bareResult.Data!.ExitCode != 0)
        {
            return Result.Fail<RepositoryDescriptor>(
                GitDiagnosticClassifier.ClassifyInspectionFailure(bareResult.Data));
        }

        var bareParseResult = GitOutputParser.ParseBoolean(bareResult.Data);
        if (bareParseResult.IsFailure)
        {
            return Result.ErrorFromResult<RepositoryDescriptor>(bareParseResult);
        }

        var isInsideWorkTree = insideParseResult.Data;
        var isBare = bareParseResult.Data;
        if (isBare)
        {
            return Result.Fail<RepositoryDescriptor>(
                OperationError.UnprocessableInput(
                    "The selected repository does not have a working tree.",
                    RepositoryErrorCode.WorkTreeUnavailable));
        }

        if (!isInsideWorkTree)
        {
            return Result.Fail<RepositoryDescriptor>(
                OperationError.UnprocessableInput(
                    "The selected folder is not inside a Git working tree.",
                    RepositoryErrorCode.NotGitRepository));
        }

        var rootResult = await RunAsync(
            startedAt,
            ["-C", selectedPath, "rev-parse", "--show-toplevel"],
            cancellationToken);
        if (rootResult.IsFailure)
        {
            return Result.ErrorFromResult<RepositoryDescriptor>(rootResult);
        }

        if (rootResult.Data!.ExitCode != 0)
        {
            return Result.Fail<RepositoryDescriptor>(
                GitDiagnosticClassifier.ClassifyInspectionFailure(rootResult.Data));
        }

        var rootParseResult = GitOutputParser.ParsePath(rootResult.Data);
        if (rootParseResult.IsFailure)
        {
            return Result.ErrorFromResult<RepositoryDescriptor>(rootParseResult);
        }

        var canonicalRootResult = await pathResolver.ResolveAsync(
            rootParseResult.Data!,
            cancellationToken);
        if (canonicalRootResult.IsFailure)
        {
            return Result.ErrorFromResult<RepositoryDescriptor>(canonicalRootResult);
        }

        var canonicalRoot = canonicalRootResult.Data!;
        var revisionResult = await RunAsync(
            startedAt,
            ["-C", canonicalRoot, "rev-parse", "--verify", "HEAD^{commit}"],
            cancellationToken);
        if (revisionResult.IsFailure)
        {
            return Result.ErrorFromResult<RepositoryDescriptor>(revisionResult);
        }

        if (revisionResult.Data!.ExitCode != 0)
        {
            return Result.Fail<RepositoryDescriptor>(
                GitDiagnosticClassifier.ClassifyInspectionFailure(revisionResult.Data));
        }

        var revisionParseResult = GitOutputParser.ParseRevision(revisionResult.Data);
        if (revisionParseResult.IsFailure)
        {
            return Result.ErrorFromResult<RepositoryDescriptor>(revisionParseResult);
        }

        var headResult = await RunAsync(
            startedAt,
            ["-C", canonicalRoot, "symbolic-ref", "--quiet", "--short", "HEAD"],
            cancellationToken);
        if (headResult.IsFailure)
        {
            return Result.ErrorFromResult<RepositoryDescriptor>(headResult);
        }

        var headParseResult = GitOutputParser.ParseHead(
            headResult.Data!,
            revisionParseResult.Data!);
        if (headParseResult.IsFailure)
        {
            return Result.ErrorFromResult<RepositoryDescriptor>(headParseResult);
        }

        var name = new DirectoryInfo(canonicalRoot).Name;
        if (name.Length == 0)
        {
            name = canonicalRoot;
        }

        return Result.Success(
            new RepositoryDescriptor(
                name,
                canonicalRoot,
                headParseResult.Data!));
    }

    /// <summary>
    ///     Runs one Git fact with the time remaining in the shared inspection budget.
    /// </summary>
    /// <param name="startedAt">The monotonic timestamp at which the shared inspection budget began.</param>
    /// <param name="arguments">The approved Git arguments. Cannot be <see langword="null" />.</param>
    /// <param name="cancellationToken">
    ///     A <see cref="CancellationToken" /> to observe while waiting for the task to complete.
    /// </param>
    /// <returns>
    ///     A task that represents the asynchronous operation. The task result contains the bounded Git output on success.
    /// </returns>
    private Task<Result<GitCommandOutput>> RunAsync(
        long startedAt,
        IReadOnlyList<string> arguments,
        CancellationToken cancellationToken)
    {
        var remaining = GitInspectionConstants.InspectionTimeout - Stopwatch.GetElapsedTime(startedAt);
        if (remaining <= TimeSpan.Zero)
        {
            return Task.FromResult(TimedOut<GitCommandOutput>());
        }

        return commandRunner.RunAsync(
            new GitCommand(arguments, remaining, GitInspectionConstants.MaximumStreamBytes),
            cancellationToken);
    }

    /// <summary>
    ///     Validates the repository path shape and Unicode scalar bound.
    /// </summary>
    /// <param name="path">The selected path, or <see langword="null" /> when none was provided.</param>
    /// <returns>A successful result when the path is valid; otherwise, the stable invalid-path failure.</returns>
    private static Result ValidatePath(string? path)
    {
        if (string.IsNullOrWhiteSpace(path) || path.Contains('\0'))
        {
            return InvalidPath();
        }

        var remaining = path.AsSpan();
        var scalarCount = 0;
        while (!remaining.IsEmpty)
        {
            var status = Rune.DecodeFromUtf16(remaining, out _, out var charactersConsumed);
            if (status != OperationStatus.Done)
            {
                return InvalidPath();
            }

            scalarCount++;
            if (scalarCount > 8_192)
            {
                return InvalidPath();
            }

            remaining = remaining[charactersConsumed..];
        }

        return Result.Success();
    }

    /// <summary>
    ///     Creates the stable invalid repository path failure.
    /// </summary>
    /// <returns>A failed result with the repository invalid-path error.</returns>
    private static Result InvalidPath() =>
        Result.Fail(
            OperationError.Validation(
                "The selected repository path is invalid.",
                RepositoryErrorCode.InvalidPath));

    /// <summary>
    ///     Creates the stable Git inspection timeout failure.
    /// </summary>
    /// <typeparam name="T">The success payload type.</typeparam>
    /// <returns>A failed result with the Git timeout error.</returns>
    private static Result<T> TimedOut<T>() =>
        Result.Fail<T>(
            OperationError.Timeout(
                "Git repository inspection exceeded its allowed time.",
                GitErrorCode.TimedOut));
}
