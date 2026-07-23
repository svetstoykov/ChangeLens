using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.Text;
using ChangeLens.Core.Git.Constants;
using ChangeLens.Core.Git.Interfaces;
using ChangeLens.Core.Git.Models;
using ChangeLens.Core.Repositories.Constants;
using ChangeLens.Core.Results.Models;
using ChangeLens.Infrastructure.Git.Constants;

namespace ChangeLens.Infrastructure.Git.Services;

/// <summary>
///     Runs bounded, read-only Git commands without invoking a shell.
/// </summary>
/// <remarks>
///     <para>
///         This implementation is stateless and safe to register as a singleton.
///     </para>
///     <para>
///         Git arguments are passed as distinct process arguments. Paging, prompts, optional locks, and locale-dependent
///         output are disabled for every execution.
///     </para>
///     <para>
///         Deadlines and caller cancellation cover both process exit and redirected-stream completion. Descendant
///         cleanup is best effort after the tracked process exits because the platform process API no longer retains a
///         handle to that descendant tree.
///     </para>
/// </remarks>
public sealed class GitCliCommandRunner : IGitCommandRunner
{
    /// <summary>
    ///     Rejects malformed UTF-8 output instead of replacing invalid bytes.
    /// </summary>
    private static readonly UTF8Encoding StrictUtf8 = new(
        encoderShouldEmitUTF8Identifier: false,
        throwOnInvalidBytes: true);

    private readonly string _executablePath;
    private readonly ReadOnlyCollection<string> _executableArguments;

    /// <summary>
    ///     Initializes a new instance of the <see cref="GitCliCommandRunner" /> class using the installed Git executable.
    /// </summary>
    public GitCliCommandRunner()
        : this(GitProcessConstants.DefaultExecutable, [])
    {
    }

    /// <summary>
    ///     Initializes a new instance of the <see cref="GitCliCommandRunner" /> class using a configured executable.
    /// </summary>
    /// <param name="executablePath">
    ///     The configured executable path or name. Cannot be <see langword="null" /> or empty.
    /// </param>
    /// <param name="executableArguments">
    ///     The immutable prefix arguments placed before every Git argument. Cannot be <see langword="null" /> or contain
    ///     <see langword="null" /> values.
    /// </param>
    /// <exception cref="ArgumentException">
    ///     <paramref name="executablePath" /> is empty or contains only white-space characters.
    ///     -or-
    ///     <paramref name="executableArguments" /> contains a <see langword="null" /> value.
    /// </exception>
    /// <exception cref="ArgumentNullException">
    ///     <paramref name="executablePath" /> is <see langword="null" />.
    ///     -or-
    ///     <paramref name="executableArguments" /> is <see langword="null" />.
    /// </exception>
    public GitCliCommandRunner(
        string executablePath,
        IEnumerable<string> executableArguments)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(executablePath);
        ArgumentNullException.ThrowIfNull(executableArguments);

        var copiedArguments = executableArguments.ToArray();
        if (copiedArguments.Any(argument => argument is null))
        {
            throw new ArgumentException(
                "Executable arguments cannot contain null values.",
                nameof(executableArguments));
        }

        _executablePath = executablePath;
        _executableArguments = Array.AsReadOnly(copiedArguments);
    }

    /// <inheritdoc />
    public async Task<Result<GitCommandOutput>> RunAsync(
        GitCommand command,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);
        cancellationToken.ThrowIfCancellationRequested();

        using var process = new Process
        {
            StartInfo = CreateStartInfo(command),
        };

        try
        {
            if (!process.Start())
            {
                return Unavailable();
            }
        }
        catch (Exception exception) when (
            exception is Win32Exception
                or FileNotFoundException
                or DirectoryNotFoundException
                or UnauthorizedAccessException
                or InvalidOperationException)
        {
            return Unavailable();
        }

        using var timeout = new CancellationTokenSource(command.Timeout);
        using var executionCancellation = CancellationTokenSource.CreateLinkedTokenSource(
            cancellationToken,
            timeout.Token);
        var standardOutputTask = ReadBoundedAsync(
            process.StandardOutput.BaseStream,
            command.MaximumStreamBytes,
            executionCancellation.Token);
        var standardErrorTask = ReadBoundedAsync(
            process.StandardError.BaseStream,
            command.MaximumStreamBytes,
            executionCancellation.Token);
        var exitTask = process.WaitForExitAsync(CancellationToken.None);
        var completionTask = WaitForCompletionOrLimitAsync(
            exitTask,
            standardOutputTask,
            standardErrorTask,
            command.MaximumStreamBytes);
        Task[] cleanupTasks =
        [
            exitTask,
            standardOutputTask,
            standardErrorTask,
            completionTask,
        ];

        try
        {
            var exceededLimit = await completionTask.WaitAsync(executionCancellation.Token);
            if (exceededLimit)
            {
                await TerminateAndCleanUpAsync(process, executionCancellation, cleanupTasks);
                return InspectionFailed();
            }

            var standardOutputBytes = await standardOutputTask;
            var standardErrorBytes = await standardErrorTask;
            if (standardOutputBytes.Length > command.MaximumStreamBytes
                || standardErrorBytes.Length > command.MaximumStreamBytes)
            {
                return InspectionFailed();
            }

            try
            {
                return Result.Success(
                    new GitCommandOutput(
                        process.ExitCode,
                        StrictUtf8.GetString(standardOutputBytes),
                        StrictUtf8.GetString(standardErrorBytes)));
            }
            catch (DecoderFallbackException)
            {
                return InspectionFailed();
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            await TerminateAndCleanUpAsync(process, executionCancellation, cleanupTasks);
            cancellationToken.ThrowIfCancellationRequested();
            throw;
        }
        catch (OperationCanceledException) when (timeout.IsCancellationRequested)
        {
            await TerminateAndCleanUpAsync(process, executionCancellation, cleanupTasks);
            return TimedOut();
        }
        catch (IOException)
        {
            await TerminateAndCleanUpAsync(process, executionCancellation, cleanupTasks);
            return InspectionFailed();
        }
    }

    /// <summary>
    ///     Creates the direct process configuration with immutable argument boundaries and the safe Git environment.
    /// </summary>
    /// <param name="command">The immutable Git command. Cannot be <see langword="null" />.</param>
    /// <returns>The configured shell-free process start information.</returns>
    private ProcessStartInfo CreateStartInfo(GitCommand command)
    {
        var startInfo = new ProcessStartInfo(_executablePath)
        {
            CreateNoWindow = true,
            RedirectStandardError = true,
            RedirectStandardOutput = true,
            UseShellExecute = false,
        };

        foreach (var argument in _executableArguments)
        {
            startInfo.ArgumentList.Add(argument);
        }

        foreach (var argument in command.Arguments)
        {
            startInfo.ArgumentList.Add(argument);
        }

        startInfo.Environment[GitProcessConstants.OptionalLocksEnvironmentVariable] =
            GitProcessConstants.DisabledEnvironmentValue;
        startInfo.Environment[GitProcessConstants.TerminalPromptEnvironmentVariable] =
            GitProcessConstants.DisabledEnvironmentValue;
        startInfo.Environment[GitProcessConstants.CredentialInteractionEnvironmentVariable] =
            GitProcessConstants.NonInteractiveCredentialValue;
        startInfo.Environment[GitProcessConstants.GitPagerEnvironmentVariable] =
            GitProcessConstants.NonInteractivePagerValue;
        startInfo.Environment[GitProcessConstants.PagerEnvironmentVariable] =
            GitProcessConstants.NonInteractivePagerValue;
        startInfo.Environment[GitProcessConstants.LocaleEnvironmentVariable] =
            GitProcessConstants.InvariantLocaleValue;
        startInfo.Environment[GitProcessConstants.LanguageEnvironmentVariable] =
            GitProcessConstants.InvariantLocaleValue;
        return startInfo;
    }

    /// <summary>
    ///     Waits for process exit and stream completion while detecting either stream that reaches the rejection
    ///     sentinel.
    /// </summary>
    /// <param name="exitTask">The process-exit task. Cannot be <see langword="null" />.</param>
    /// <param name="standardOutputTask">
    ///     The bounded standard-output capture task. Cannot be <see langword="null" />.
    /// </param>
    /// <param name="standardErrorTask">
    ///     The bounded standard-error capture task. Cannot be <see langword="null" />.
    /// </param>
    /// <param name="maximumStreamBytes">The maximum accepted byte count for either stream.</param>
    /// <returns>
    ///     A task that represents the asynchronous operation. The task result is <see langword="true" /> when either
    ///     stream exceeds the bound; otherwise, <see langword="false" />.
    /// </returns>
    private static async Task<bool> WaitForCompletionOrLimitAsync(
        Task exitTask,
        Task<byte[]> standardOutputTask,
        Task<byte[]> standardErrorTask,
        int maximumStreamBytes)
    {
        var pendingTasks = new List<Task>
        {
            exitTask,
            standardOutputTask,
            standardErrorTask,
        };

        while (pendingTasks.Count > 0)
        {
            var completedTask = await Task.WhenAny(pendingTasks);
            pendingTasks.Remove(completedTask);

            if (completedTask == exitTask)
            {
                await exitTask;
                continue;
            }

            var bytes = completedTask == standardOutputTask
                ? await standardOutputTask
                : await standardErrorTask;
            if (bytes.Length > maximumStreamBytes)
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    ///     Captures at most one byte beyond the accepted stream bound.
    /// </summary>
    /// <param name="stream">The redirected process stream. Cannot be <see langword="null" />.</param>
    /// <param name="maximumStreamBytes">The maximum accepted byte count.</param>
    /// <param name="cancellationToken">
    ///     A <see cref="CancellationToken" /> to observe while waiting for redirected output.
    /// </param>
    /// <returns>
    ///     A task that represents the asynchronous operation. The task result contains the bounded byte capture.
    /// </returns>
    /// <exception cref="OperationCanceledException">
    ///     If the <paramref name="cancellationToken" /> is canceled.
    /// </exception>
    private static async Task<byte[]> ReadBoundedAsync(
        Stream stream,
        int maximumStreamBytes,
        CancellationToken cancellationToken)
    {
        var captureLimit = (long)maximumStreamBytes + 1;
        var buffer = new byte[(int)Math.Min(8_192, captureLimit)];
        using var capture = new MemoryStream(buffer.Length);

        while (capture.Length < captureLimit)
        {
            var requestedBytes = (int)Math.Min(
                buffer.Length,
                captureLimit - capture.Length);
            var bytesRead = await stream.ReadAsync(
                buffer.AsMemory(0, requestedBytes),
                cancellationToken);
            if (bytesRead == 0)
            {
                break;
            }

            await capture.WriteAsync(
                buffer.AsMemory(0, bytesRead),
                cancellationToken);
        }

        return capture.ToArray();
    }

    /// <summary>
    ///     Terminates the process tree when still active, closes redirected streams, and bounds cleanup work.
    /// </summary>
    /// <remarks>
    ///     Once the tracked process has exited, its descendants cannot be discovered reliably through
    ///     <see cref="Process" />. Closing the local stream handles still guarantees that cleanup cannot hold the caller
    ///     beyond the grace period.
    /// </remarks>
    /// <param name="process">The started process. Cannot be <see langword="null" />.</param>
    /// <param name="executionCancellation">
    ///     The linked source that controls process execution. Cannot be <see langword="null" />.
    /// </param>
    /// <param name="cleanupTasks">
    ///     The process and stream tasks to observe during cleanup. Cannot be <see langword="null" />.
    /// </param>
    /// <returns>A task that represents the asynchronous operation.</returns>
    private static async Task TerminateAndCleanUpAsync(
        Process process,
        CancellationTokenSource executionCancellation,
        IReadOnlyCollection<Task> cleanupTasks)
    {
        try
        {
            await executionCancellation.CancelAsync();
        }
        catch (AggregateException)
        {
        }

        try
        {
            if (!process.HasExited)
            {
                process.Kill(entireProcessTree: true);
            }
        }
        catch (Exception exception) when (
            exception is InvalidOperationException or Win32Exception)
        {
        }

        try
        {
            await process.StandardOutput.BaseStream.DisposeAsync();
        }
        catch (Exception exception) when (
            exception is IOException or InvalidOperationException)
        {
        }

        try
        {
            await process.StandardError.BaseStream.DisposeAsync();
        }
        catch (Exception exception) when (
            exception is IOException or InvalidOperationException)
        {
        }

        var observationTasks = cleanupTasks
            .Select(ObserveCleanupTaskAsync)
            .ToArray();

        try
        {
            await Task.WhenAll(observationTasks)
                .WaitAsync(GitProcessConstants.CleanupGracePeriod);
        }
        catch (TimeoutException)
        {
        }
    }

    /// <summary>
    ///     Observes expected task failures after execution has already reached a terminal outcome.
    /// </summary>
    /// <param name="task">The cleanup task to observe. Cannot be <see langword="null" />.</param>
    /// <returns>A task that represents the asynchronous operation.</returns>
    private static async Task ObserveCleanupTaskAsync(Task task)
    {
        try
        {
            await task;
        }
        catch (Exception exception) when (
            exception is OperationCanceledException
                or IOException
                or InvalidOperationException
                or Win32Exception)
        {
        }
    }

    private static Result<GitCommandOutput> Unavailable() =>
        Result.Fail<GitCommandOutput>(
            OperationError.ExternalDependencyFailure(
                "Git is unavailable.",
                GitErrorCode.Unavailable));

    private static Result<GitCommandOutput> TimedOut() =>
        Result.Fail<GitCommandOutput>(
            OperationError.Timeout(
                "Git repository inspection exceeded its allowed time.",
                GitErrorCode.TimedOut));

    private static Result<GitCommandOutput> InspectionFailed() =>
        Result.Fail<GitCommandOutput>(
            OperationError.ExternalDependencyFailure(
                "Git repository inspection failed.",
                RepositoryErrorCode.InspectionFailed));
}
