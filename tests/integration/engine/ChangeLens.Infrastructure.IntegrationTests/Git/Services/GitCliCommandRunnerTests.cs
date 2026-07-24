using System.ComponentModel;
using System.Diagnostics;
using System.Globalization;
using System.Text;
using System.Text.Json;
using ChangeLens.Core.Git.Constants;
using ChangeLens.Core.Git.Models;
using ChangeLens.Core.Repositories.Constants;
using ChangeLens.Core.Results.Models;
using ChangeLens.Infrastructure.Git.Services;
using ChangeLens.Infrastructure.IntegrationTests.Support;
using Xunit;

namespace ChangeLens.Infrastructure.IntegrationTests.Git.Services;

/// <summary>
///     Verifies bounded direct process execution against a controlled Git-shaped fixture.
/// </summary>
public sealed class GitCliCommandRunnerTests
{
    private const int MaximumStandardOutputBytes = 8 * 1024 * 1024;
    private const int MaximumStandardErrorBytes = 64 * 1024;
    private static readonly TimeSpan DefaultTimeout = TimeSpan.FromSeconds(10);

    /// <summary>
    ///     Asynchronously preserves argument boundaries and applies the required non-interactive environment.
    /// </summary>
    /// <returns>A task that represents the asynchronous operation.</returns>
    [Fact]
    public async Task RunAsync_ArgumentsAndEnvironment_PreservesValuesAndAppliesSafeEnvironment()
    {
        using var temporaryDirectory = new TemporaryDirectory();
        var shellMarker = Path.Combine(temporaryDirectory.DirectoryPath, "shell-marker");
        var arguments = new[]
        {
            "argument with spaces",
            "{value}",
            "Здравей 🌍",
            $"$(touch {shellMarker})",
        };
        var runner = CreateRunner();

        var result = await RunInFixtureModeAsync(
            "inspect",
            () => runner.RunAsync(CreateCommand(arguments), CancellationToken.None));

        Assert.True(result.IsSuccess);
        Assert.False(File.Exists(shellMarker));
        using var document = JsonDocument.Parse(Assert.IsType<GitCommandOutput>(result.Data).StandardOutput);
        var receivedArguments = document.RootElement
            .GetProperty("arguments")
            .EnumerateArray()
            .Select(element => element.GetString())
            .ToArray();
        Assert.Equal(arguments, receivedArguments);

        var environment = document.RootElement.GetProperty("environment");
        Assert.Equal("0", environment.GetProperty("GIT_OPTIONAL_LOCKS").GetString());
        Assert.Equal("0", environment.GetProperty("GIT_TERMINAL_PROMPT").GetString());
        Assert.Equal("Never", environment.GetProperty("GCM_INTERACTIVE").GetString());
        Assert.Equal("cat", environment.GetProperty("GIT_PAGER").GetString());
        Assert.Equal("cat", environment.GetProperty("PAGER").GetString());
        Assert.Equal("C", environment.GetProperty("LC_ALL").GetString());
        Assert.Equal("C", environment.GetProperty("LANG").GetString());
        Assert.Equal(string.Empty, environment.GetProperty("GIT_EXTERNAL_DIFF").GetString());
    }

    /// <summary>
    ///     Asynchronously copies executable prefix arguments before process execution.
    /// </summary>
    /// <returns>A task that represents the asynchronous operation.</returns>
    [Fact]
    public async Task Constructor_MutablePrefixArguments_CopiesValues()
    {
        var executableArguments = new[] { FixtureAssemblyPath };
        var runner = new GitCliCommandRunner(DotnetExecutablePath, executableArguments);
        executableArguments[0] = "changed-after-construction.dll";

        var result = await RunInFixtureModeAsync(
            "success",
            () => runner.RunAsync(CreateCommand([]), CancellationToken.None));

        Assert.True(result.IsSuccess);
    }

    /// <summary>
    ///     Asynchronously captures standard output and standard error separately for a successful process.
    /// </summary>
    /// <returns>A task that represents the asynchronous operation.</returns>
    [Fact]
    public async Task RunAsync_Success_CapturesStreamsSeparately()
    {
        var runner = CreateRunner();

        var result = await RunInFixtureModeAsync(
            "success",
            () => runner.RunAsync(CreateCommand([]), CancellationToken.None));

        Assert.True(result.IsSuccess);
        var output = Assert.IsType<GitCommandOutput>(result.Data);
        Assert.Equal(0, output.ExitCode);
        Assert.Equal("fixture standard output", output.StandardOutput);
        Assert.Equal("fixture standard error", output.StandardError);
    }

    /// <summary>
    ///     Asynchronously returns nonzero process exits as captured Git output.
    /// </summary>
    /// <returns>A task that represents the asynchronous operation.</returns>
    [Fact]
    public async Task RunAsync_NonzeroExit_ReturnsCapturedOutput()
    {
        var runner = CreateRunner();

        var result = await RunInFixtureModeAsync(
            "nonzero",
            () => runner.RunAsync(CreateCommand([]), CancellationToken.None));

        Assert.True(result.IsSuccess);
        var output = Assert.IsType<GitCommandOutput>(result.Data);
        Assert.Equal(128, output.ExitCode);
        Assert.Equal("fixture nonzero output", output.StandardOutput);
        Assert.Equal("fixture nonzero error", output.StandardError);
    }

    /// <summary>
    ///     Asynchronously returns the stable unavailable error when the configured executable is missing.
    /// </summary>
    /// <returns>A task that represents the asynchronous operation.</returns>
    [Fact]
    public async Task RunAsync_MissingExecutable_ReturnsUnavailableError()
    {
        var runner = new GitCliCommandRunner(
            Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"), "missing-git"),
            []);

        var result = await runner.RunAsync(CreateCommand([]), CancellationToken.None);

        AssertFailure(result, ErrorType.ExternalDependencyFailure, GitErrorCode.Unavailable);
    }

    /// <summary>
    ///     Asynchronously rejects output streams that exceed the configured byte limit.
    /// </summary>
    /// <param name="mode">The fixture mode that selects the oversized stream.</param>
    /// <returns>A task that represents the asynchronous operation.</returns>
    [Theory]
    [InlineData("large-stdout")]
    [InlineData("large-stderr")]
    public async Task RunAsync_StreamExceedingItsOwnBound_ReturnsSuppliedOutputLimitError(string mode)
    {
        var runner = CreateRunner();
        var outputLimitError = OperationError.UnprocessableInput("Output limit.", "comparison.tooLarge");

        var result = await RunInFixtureModeAsync(
            mode,
            () => runner.RunAsync(
                CreateCommand(
                    [],
                    maximumStandardOutputBytes: mode == "large-stdout" ? 64 * 1024 : null,
                    outputLimitError: outputLimitError),
                CancellationToken.None));

        var error = Assert.Single(result.Errors);
        Assert.Same(outputLimitError, error);
        Assert.DoesNotContain(new string('x', 32), error.Message, StringComparison.Ordinal);
    }

    /// <summary>
    ///     Asynchronously allows large standard output below its separate configured ceiling.
    /// </summary>
    /// <returns>A task that represents the asynchronous operation.</returns>
    [Fact]
    public async Task RunAsync_LargeStandardOutputBelowSeparateCeiling_Succeeds()
    {
        var runner = CreateRunner();

        var result = await RunInFixtureModeAsync(
            "large-stdout",
            () => runner.RunAsync(CreateCommand([]), CancellationToken.None));

        Assert.True(result.IsSuccess);
        Assert.Equal(128 * 1024, Assert.IsType<GitCommandOutput>(result.Data).StandardOutput.Length);
    }

    /// <summary>
    ///     Asynchronously preserves exactly 8 MiB of committed-file facts and an independent 64 KiB diagnostic stream.
    /// </summary>
    /// <returns>A task that represents the asynchronous operation.</returns>
    [Fact]
    public async Task RunAsync_ExactComparisonFactAndDiagnosticLimits_Succeeds()
    {
        var runner = CreateRunner();

        var result = await RunInFixtureModeAsync(
            "raw-committed-records",
            new Dictionary<string, string>
            {
                ["CHANGELENS_GIT_FIXTURE_RECORD_COUNT"] = "8192",
                ["CHANGELENS_GIT_FIXTURE_PATH_LENGTH"] = "924",
                ["CHANGELENS_GIT_FIXTURE_STDERR_BYTES"] = (64 * 1024).ToString(CultureInfo.InvariantCulture),
            },
            () => runner.RunAsync(CreateCommand([]), CancellationToken.None));

        Assert.True(result.IsSuccess);
        var output = Assert.IsType<GitCommandOutput>(result.Data);
        Assert.Equal(MaximumStandardOutputBytes, Encoding.UTF8.GetByteCount(output.StandardOutput));
        Assert.Equal(MaximumStandardErrorBytes, Encoding.UTF8.GetByteCount(output.StandardError));
    }

    /// <summary>
    ///     Asynchronously rejects an 8 MiB-plus-one comparison fact stream without exposing partial data and reaps its child.
    /// </summary>
    /// <returns>A task that represents the asynchronous operation.</returns>
    [Fact]
    public async Task RunAsync_ComparisonFactOneByteOverLimit_ReturnsTooLargeWithoutPartialDataAndReapsChild()
    {
        using var temporaryDirectory = new TemporaryDirectory();
        var childProcessIdPath = Path.Combine(temporaryDirectory.DirectoryPath, "child.pid");
        var tooLarge = OperationError.UnprocessableInput("Output limit.", "comparison.tooLarge");
        var runner = CreateRunner();

        try
        {
            var result = await RunInFixtureModeAsync(
                "raw-committed-records",
                new Dictionary<string, string>
                {
                    ["CHANGELENS_GIT_FIXTURE_RECORD_COUNT"] = "8192",
                    ["CHANGELENS_GIT_FIXTURE_PATH_LENGTH"] = "924",
                    ["CHANGELENS_GIT_FIXTURE_EXTRA_STDOUT_BYTES"] = "1",
                    ["CHANGELENS_GIT_FIXTURE_STDERR_BYTES"] = (64 * 1024).ToString(CultureInfo.InvariantCulture),
                    ["CHANGELENS_GIT_FIXTURE_CHILD_PROCESS_ID_PATH"] = childProcessIdPath,
                    ["CHANGELENS_GIT_FIXTURE_WAIT_AFTER_WRITE"] = "true",
                },
                () => runner.RunAsync(
                    CreateCommand([], outputLimitError: tooLarge),
                    CancellationToken.None));

            Assert.True(result.IsFailure);
            Assert.Same(tooLarge, Assert.Single(result.Errors));
            Assert.Null(result.Data);
            await AssertProcessReapedAsync(await ReadProcessIdAsync(childProcessIdPath));
        }
        finally
        {
            await TerminateRecordedProcessAsync(childProcessIdPath);
        }
    }

    /// <summary>
    ///     Asynchronously rejects invalid UTF-8 output without exposing captured bytes.
    /// </summary>
    /// <returns>A task that represents the asynchronous operation.</returns>
    [Fact]
    public async Task RunAsync_InvalidUtf8_ReturnsSuppliedInspectionFailure()
    {
        var runner = CreateRunner();
        var inspectionError = OperationError.ExternalDependencyFailure("Inspection failed.", "comparison.failed");

        var result = await RunInFixtureModeAsync(
            "invalid-utf8",
            () => runner.RunAsync(CreateCommand([], inspectionError: inspectionError), CancellationToken.None));

        var error = Assert.Single(result.Errors);
        Assert.Same(inspectionError, error);
        Assert.DoesNotContain("�", error.Message, StringComparison.Ordinal);
    }

    /// <summary>
    ///     Asynchronously returns the supplied inspection failure when reading a redirected stream fails.
    /// </summary>
    /// <returns>A task that represents the asynchronous operation.</returns>
    [Fact]
    public async Task RunAsync_RedirectedStreamReadFailure_ReturnsSuppliedInspectionFailure()
    {
        var inspectionError = OperationError.ExternalDependencyFailure("Inspection failed.", "comparison.failed");
        var runner = new GitCliCommandRunner(
            DotnetExecutablePath,
            [FixtureAssemblyPath],
            ThrowRedirectedStreamReadFailureAsync);

        var result = await RunInFixtureModeAsync(
            "sleep",
            () => runner.RunAsync(CreateCommand([], inspectionError: inspectionError), CancellationToken.None));

        Assert.Same(inspectionError, Assert.Single(result.Errors));
    }

    /// <summary>
    ///     Asynchronously terminates and reaps the controlled process tree when a stream limit is exceeded.
    /// </summary>
    /// <returns>A task that represents the asynchronous operation.</returns>
    [Fact]
    public async Task RunAsync_OutputLimit_KillsAndReapsProcessTree()
    {
        using var temporaryDirectory = new TemporaryDirectory();
        var childProcessIdPath = Path.Combine(temporaryDirectory.DirectoryPath, "child.pid");
        var outputLimitError = OperationError.UnprocessableInput("Output limit.", "comparison.tooLarge");
        var runner = CreateRunner();

        try
        {
            var result = await RunInFixtureModeAsync(
                "spawn-child-and-large-stdout",
                () => runner.RunAsync(
                    CreateCommand(
                        [childProcessIdPath],
                        maximumStandardOutputBytes: 64 * 1024,
                        outputLimitError: outputLimitError),
                    CancellationToken.None));

            Assert.Same(outputLimitError, Assert.Single(result.Errors));
            var childProcessId = await ReadProcessIdAsync(childProcessIdPath);
            await AssertProcessReapedAsync(childProcessId);
        }
        finally
        {
            await TerminateRecordedProcessAsync(childProcessIdPath);
        }
    }

    /// <summary>
    ///     Asynchronously times out, terminates, and reaps the controlled process tree.
    /// </summary>
    /// <returns>A task that represents the asynchronous operation.</returns>
    [Fact]
    public async Task RunAsync_InternalTimeout_KillsAndReapsProcessTree()
    {
        using var temporaryDirectory = new TemporaryDirectory();
        var childProcessIdPath = Path.Combine(temporaryDirectory.DirectoryPath, "child.pid");
        var runner = CreateRunner();
        var timedOut = OperationError.Timeout("Timed out.", "comparison.timedOut");

        var result = await RunInFixtureModeAsync(
            "spawn-child",
            () => runner.RunAsync(
                CreateCommand([childProcessIdPath], TimeSpan.FromSeconds(2), timedOutError: timedOut),
                CancellationToken.None));

        Assert.Same(timedOut, Assert.Single(result.Errors));
        var childProcessId = await ReadProcessIdAsync(childProcessIdPath);
        await AssertProcessReapedAsync(childProcessId);
    }

    /// <summary>
    ///     Asynchronously terminates and reaps the controlled process tree when caller cancellation wins.
    /// </summary>
    /// <returns>A task that represents the asynchronous operation.</returns>
    [Fact]
    public async Task RunAsync_CallerCancellation_KillsAndReapsProcessTreeAndRethrows()
    {
        using var temporaryDirectory = new TemporaryDirectory();
        var childProcessIdPath = Path.Combine(temporaryDirectory.DirectoryPath, "child.pid");
        using var cancellationTokenSource = new CancellationTokenSource(TimeSpan.FromSeconds(2));
        var runner = CreateRunner();

        var exception = await Assert.ThrowsAsync<OperationCanceledException>(
            () => RunInFixtureModeAsync(
                "spawn-child",
                () => runner.RunAsync(
                    CreateCommand([childProcessIdPath], TimeSpan.FromSeconds(10)),
                    cancellationTokenSource.Token)));

        Assert.Equal(cancellationTokenSource.Token, exception.CancellationToken);
        var childProcessId = await ReadProcessIdAsync(childProcessIdPath);
        await AssertProcessReapedAsync(childProcessId);
    }

    /// <summary>
    ///     Asynchronously honors the internal deadline after the root exits while a child retains the redirected
    ///     streams.
    /// </summary>
    /// <returns>A task that represents the asynchronous operation.</returns>
    [Fact]
    public async Task RunAsync_RootExitsWithInheritingChild_InternalTimeoutCompletesWithinBound()
    {
        using var temporaryDirectory = new TemporaryDirectory();
        var childProcessIdPath = Path.Combine(temporaryDirectory.DirectoryPath, "child.pid");
        var runner = CreateRunner();

        try
        {
            var result = await RunInFixtureModeAsync(
                    "spawn-inheriting-child-and-exit",
                    () => runner.RunAsync(
                        CreateCommand([childProcessIdPath], TimeSpan.FromSeconds(1)),
                        CancellationToken.None))
                .WaitAsync(
                    TimeSpan.FromSeconds(5),
                    TestContext.Current.CancellationToken);

            AssertFailure(result, ErrorType.Timeout, GitErrorCode.TimedOut);
        }
        finally
        {
            await TerminateRecordedProcessAsync(childProcessIdPath);
        }
    }

    /// <summary>
    ///     Asynchronously honors caller cancellation after the root exits while a child retains redirected streams.
    /// </summary>
    /// <returns>A task that represents the asynchronous operation.</returns>
    [Fact]
    public async Task RunAsync_RootExitsWithInheritingChild_CallerCancellationCompletesWithinBound()
    {
        using var temporaryDirectory = new TemporaryDirectory();
        var childProcessIdPath = Path.Combine(temporaryDirectory.DirectoryPath, "child.pid");
        using var cancellationTokenSource = new CancellationTokenSource(TimeSpan.FromSeconds(1));
        var runner = CreateRunner();

        try
        {
            var exception = await Assert.ThrowsAsync<OperationCanceledException>(
                () => RunInFixtureModeAsync(
                        "spawn-inheriting-child-and-exit",
                        () => runner.RunAsync(
                            CreateCommand([childProcessIdPath], TimeSpan.FromSeconds(10)),
                            cancellationTokenSource.Token))
                    .WaitAsync(
                        TimeSpan.FromSeconds(5),
                        TestContext.Current.CancellationToken));

            Assert.Equal(cancellationTokenSource.Token, exception.CancellationToken);
        }
        finally
        {
            await TerminateRecordedProcessAsync(childProcessIdPath);
        }
    }

    private static string DotnetExecutablePath =>
        Environment.GetEnvironmentVariable("DOTNET_HOST_PATH") ?? "dotnet";

    private static string FixtureAssemblyPath =>
        Path.Combine(AppContext.BaseDirectory, "ChangeLens.GitProcessFixture.dll");

    private static GitCliCommandRunner CreateRunner() =>
        new(DotnetExecutablePath, [FixtureAssemblyPath]);

    private static Task<byte[]> ThrowRedirectedStreamReadFailureAsync(
        Stream stream,
        int maximumStreamBytes,
        CancellationToken cancellationToken) =>
        Task.FromException<byte[]>(new IOException("controlled redirected-stream read failure"));

    private static GitCommand CreateCommand(
        IReadOnlyList<string> arguments,
        TimeSpan? timeout = null,
        int? maximumStandardOutputBytes = null,
        int? maximumStandardErrorBytes = null,
        OperationError? timedOutError = null,
        OperationError? outputLimitError = null,
        OperationError? inspectionError = null) =>
        new(
            arguments,
            timeout ?? DefaultTimeout,
            maximumStandardOutputBytes ?? MaximumStandardOutputBytes,
            maximumStandardErrorBytes ?? MaximumStandardErrorBytes,
            CreatePolicy(timedOutError, outputLimitError, inspectionError));

    private static GitCommandErrorPolicy CreatePolicy(
        OperationError? timedOutError = null,
        OperationError? outputLimitError = null,
        OperationError? inspectionError = null) =>
        new(
            timedOutError ?? OperationError.Timeout(
                "Git repository inspection exceeded its allowed time.",
                GitErrorCode.TimedOut),
            outputLimitError ?? OperationError.ExternalDependencyFailure(
                "Git repository inspection failed.",
                RepositoryErrorCode.InspectionFailed),
            inspectionError ?? OperationError.ExternalDependencyFailure(
                "Git repository inspection failed.",
                RepositoryErrorCode.InspectionFailed));

    private static async Task<T> RunInFixtureModeAsync<T>(
        string mode,
        Func<Task<T>> operation)
        => await RunInFixtureModeAsync(
            mode,
            new Dictionary<string, string>(StringComparer.Ordinal),
            operation);

    private static async Task<T> RunInFixtureModeAsync<T>(
        string mode,
        IReadOnlyDictionary<string, string> variables,
        Func<Task<T>> operation)
    {
        const string variableName = "CHANGELENS_GIT_FIXTURE_MODE";
        var previousMode = Environment.GetEnvironmentVariable(variableName);
        var previousValues = variables.ToDictionary(
            pair => pair.Key,
            pair => Environment.GetEnvironmentVariable(pair.Key),
            StringComparer.Ordinal);
        Environment.SetEnvironmentVariable(variableName, mode);
        foreach (var (name, value) in variables)
        {
            Environment.SetEnvironmentVariable(name, value);
        }

        try
        {
            return await operation();
        }
        finally
        {
            Environment.SetEnvironmentVariable(variableName, previousMode);
            foreach (var (name, value) in previousValues)
            {
                Environment.SetEnvironmentVariable(name, value);
            }
        }
    }

    private static OperationError AssertFailure(
        Result<GitCommandOutput> result,
        ErrorType expectedType,
        string expectedCode)
    {
        Assert.True(result.IsFailure);
        var error = Assert.Single(result.Errors);
        Assert.Equal(expectedType, error.Type);
        Assert.Equal(expectedCode, error.Code);
        return error;
    }

    private static async Task<int> ReadProcessIdAsync(string path)
    {
        var deadline = Stopwatch.GetTimestamp() + (long)(Stopwatch.Frequency * 5);
        while (!File.Exists(path) && Stopwatch.GetTimestamp() < deadline)
        {
            await Task.Delay(25);
        }

        var value = await File.ReadAllTextAsync(path, TestContext.Current.CancellationToken);
        return int.Parse(value, CultureInfo.InvariantCulture);
    }

    private static async Task AssertProcessReapedAsync(int processId)
    {
        var deadline = Stopwatch.GetTimestamp() + (long)(Stopwatch.Frequency * 5);

        while (Stopwatch.GetTimestamp() < deadline)
        {
            try
            {
                using var process = Process.GetProcessById(processId);
                _ = process.HasExited;
            }
            catch (ArgumentException)
            {
                return;
            }

            await Task.Delay(25);
        }

        Assert.Fail($"Process {processId} was not reaped within the allowed time.");
    }

    /// <summary>
    ///     Asynchronously terminates the descendant recorded by a fixture that intentionally outlives its parent.
    /// </summary>
    /// <param name="path">The path containing the descendant process identifier.</param>
    /// <returns>A task that represents the asynchronous operation.</returns>
    private static async Task TerminateRecordedProcessAsync(string path)
    {
        if (!File.Exists(path))
        {
            return;
        }

        var value = await File.ReadAllTextAsync(path, CancellationToken.None);
        if (!int.TryParse(value, CultureInfo.InvariantCulture, out var processId))
        {
            return;
        }

        try
        {
            using var process = Process.GetProcessById(processId);
            if (!process.HasExited)
            {
                process.Kill(entireProcessTree: true);
            }

            await process.WaitForExitAsync(CancellationToken.None)
                .WaitAsync(TimeSpan.FromSeconds(5));
        }
        catch (Exception exception) when (
            exception is ArgumentException
                or InvalidOperationException
                or Win32Exception
                or TimeoutException)
        {
        }
    }
}
