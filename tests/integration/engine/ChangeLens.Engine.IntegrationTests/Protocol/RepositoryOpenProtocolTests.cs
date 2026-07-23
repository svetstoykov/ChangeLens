using System.Diagnostics;
using System.Globalization;
using System.Text.Json;
using ChangeLens.Core.Results.Models;
using ChangeLens.Engine.IntegrationTests.Support;
using ChangeLens.Infrastructure.FileSystem.Services;
using Xunit;

namespace ChangeLens.Engine.IntegrationTests.Protocol;

/// <summary>
///     Verifies repository-open behavior through the real Engine process protocol.
/// </summary>
public sealed class RepositoryOpenProtocolTests
{
    /// <summary>
    ///     Verifies exact branch result values and JSON property names for a controlled repository.
    /// </summary>
    [Fact]
    public async Task EngineReturnsExactBranchRepositoryResult()
    {
        var temporaryRoot = CreateTemporaryRoot();

        try
        {
            var repositoryPath = InitializeRepository(temporaryRoot, "branch repository");
            var revision = RunGitChecked(["-C", repositoryPath, "rev-parse", "--verify", "HEAD"]).Trim();
            var canonicalPath = await ResolvePhysicalPathAsync(repositoryPath);
            using var engine = StartEngine();

            await engine.StandardInput.WriteLineAsync(CreateOpenRequest("branch-request", repositoryPath));
            engine.StandardInput.Close();
            using var response = await ReadResponseAsync(engine);
            await WaitForExitAsync(engine);

            AssertRepositoryEnvelope(response.RootElement, "branch-request");
            var repository = response.RootElement.GetProperty("result").GetProperty("repository");
            AssertExactProperties(repository, "name", "canonicalPath", "head");
            Assert.Equal(new DirectoryInfo(canonicalPath).Name, repository.GetProperty("name").GetString());
            Assert.Equal(canonicalPath, repository.GetProperty("canonicalPath").GetString());
            var head = repository.GetProperty("head");
            AssertExactProperties(head, "kind", "name", "revision");
            Assert.Equal("branch", head.GetProperty("kind").GetString());
            Assert.Equal("main", head.GetProperty("name").GetString());
            Assert.Equal(revision, head.GetProperty("revision").GetString());
            Assert.Equal(0, engine.ExitCode);
            Assert.Equal(
                string.Empty,
                await engine.StandardOutput.ReadToEndAsync(TestContext.Current.CancellationToken));
        }
        finally
        {
            DeleteTemporaryRoot(temporaryRoot);
        }
    }

    /// <summary>
    ///     Verifies exact detached result values and JSON property names for a controlled repository.
    /// </summary>
    [Fact]
    public async Task EngineReturnsExactDetachedRepositoryResult()
    {
        var temporaryRoot = CreateTemporaryRoot();

        try
        {
            var repositoryPath = InitializeRepository(temporaryRoot, "detached repository");
            RunGitChecked(["-C", repositoryPath, "checkout", "--detach", "--quiet", "HEAD"]);
            var revision = RunGitChecked(["-C", repositoryPath, "rev-parse", "--verify", "HEAD"]).Trim();
            var canonicalPath = await ResolvePhysicalPathAsync(repositoryPath);
            using var engine = StartEngine();

            await engine.StandardInput.WriteLineAsync(CreateOpenRequest("detached-request", repositoryPath));
            engine.StandardInput.Close();
            using var response = await ReadResponseAsync(engine);
            await WaitForExitAsync(engine);

            AssertRepositoryEnvelope(response.RootElement, "detached-request");
            var repository = response.RootElement.GetProperty("result").GetProperty("repository");
            AssertExactProperties(repository, "name", "canonicalPath", "head");
            Assert.Equal(new DirectoryInfo(canonicalPath).Name, repository.GetProperty("name").GetString());
            Assert.Equal(canonicalPath, repository.GetProperty("canonicalPath").GetString());
            var head = repository.GetProperty("head");
            AssertExactProperties(head, "kind", "revision");
            Assert.Equal("detached", head.GetProperty("kind").GetString());
            Assert.Equal(revision, head.GetProperty("revision").GetString());
            Assert.Equal(0, engine.ExitCode);
        }
        finally
        {
            DeleteTemporaryRoot(temporaryRoot);
        }
    }

    /// <summary>
    ///     Verifies strict repository parameter rejection, including duplicate nested properties.
    /// </summary>
    [Fact]
    public async Task EngineRejectsMalformedRepositoryParameters()
    {
        var requests = new[]
        {
            """{"protocolVersion":1,"requestId":"missing","action":"repositories.open"}""",
            """{"protocolVersion":1,"requestId":"null","action":"repositories.open","parameters":null}""",
            """{"protocolVersion":1,"requestId":"array","action":"repositories.open","parameters":[]}""",
            """{"protocolVersion":1,"requestId":"scalar","action":"repositories.open","parameters":1}""",
            """{"protocolVersion":1,"requestId":"missing-path","action":"repositories.open","parameters":{}}""",
            """{"protocolVersion":1,"requestId":"wrong-type","action":"repositories.open","parameters":{"path":1}}""",
            """{"protocolVersion":1,"requestId":"wrong-case","action":"repositories.open","parameters":{"Path":"/repo"}}""",
            """{"protocolVersion":1,"requestId":"unknown","action":"repositories.open","parameters":{"path":"/repo","extra":true}}""",
            """{"protocolVersion":1,"requestId":"duplicate","action":"repositories.open","parameters":{"path":"/first","path":"/second"}}""",
        };
        using var engine = StartEngine();

        foreach (var request in requests)
        {
            await engine.StandardInput.WriteLineAsync(request);
        }

        engine.StandardInput.Close();

        foreach (var request in requests)
        {
            using var response = await ReadResponseAsync(engine);
            Assert.Equal("protocol.invalidRequest", FirstErrorCode(response));
        }

        await WaitForExitAsync(engine);
        Assert.Equal(0, engine.ExitCode);
        Assert.Equal(
            string.Empty,
            await engine.StandardOutput.ReadToEndAsync(TestContext.Current.CancellationToken));
    }

    /// <summary>
    ///     Verifies stable repository errors for non-repository, bare, and unborn selections.
    /// </summary>
    /// <param name="repositoryState">The controlled repository state to create.</param>
    /// <param name="expectedCode">The expected stable repository error code.</param>
    [Theory]
    [InlineData("non-repository", "repository.notGitRepository")]
    [InlineData("bare", "repository.workTreeUnavailable")]
    [InlineData("unborn", "repository.headUnavailable")]
    public async Task EngineReturnsStableRepositoryStateError(
        string repositoryState,
        string expectedCode)
    {
        var temporaryRoot = CreateTemporaryRoot();

        try
        {
            var selectedPath = repositoryState switch
            {
                "non-repository" => Directory.CreateDirectory(
                    Path.Combine(temporaryRoot, "ordinary directory")).FullName,
                "bare" => InitializeRepository(
                    temporaryRoot,
                    "bare repository",
                    bare: true,
                    createCommit: false),
                "unborn" => InitializeRepository(
                    temporaryRoot,
                    "unborn repository",
                    bare: false,
                    createCommit: false),
                _ => throw new ArgumentOutOfRangeException(nameof(repositoryState)),
            };
            using var engine = StartEngine();

            await engine.StandardInput.WriteLineAsync(
                CreateOpenRequest("state-request", selectedPath));
            engine.StandardInput.Close();
            using var response = await ReadResponseAsync(engine);
            await WaitForExitAsync(engine);

            Assert.Equal(expectedCode, FirstErrorCode(response));
            Assert.Equal(0, engine.ExitCode);
        }
        finally
        {
            DeleteTemporaryRoot(temporaryRoot);
        }
    }

    /// <summary>
    ///     Verifies that a configured missing Git executable returns the stable availability error.
    /// </summary>
    [Fact]
    public async Task EngineReturnsStableMissingGitError()
    {
        var temporaryRoot = CreateTemporaryRoot();

        try
        {
            var selectedPath = Directory.CreateDirectory(
                Path.Combine(temporaryRoot, "selected directory")).FullName;
            var missingExecutable = Path.Combine(temporaryRoot, "missing-git-executable");
            using var engine = StartEngine(
                environment:
                    new Dictionary<string, string?>
                    {
                        ["ChangeLens__Repositories__GitExecutable"] = missingExecutable,
                    });

            await engine.StandardInput.WriteLineAsync(
                CreateOpenRequest("missing-git-request", selectedPath));
            engine.StandardInput.Close();
            using var response = await ReadResponseAsync(engine);
            await WaitForExitAsync(engine);

            Assert.Equal("git.unavailable", FirstErrorCode(response));
            Assert.Equal(0, engine.ExitCode);
        }
        finally
        {
            DeleteTemporaryRoot(temporaryRoot);
        }
    }

    /// <summary>
    ///     Verifies that a configured sleeping Git executable returns the stable timeout error.
    /// </summary>
    [Fact]
    public async Task EngineReturnsStableGitTimeoutError()
    {
        var temporaryRoot = CreateTemporaryRoot();

        try
        {
            var selectedPath = Directory.CreateDirectory(
                Path.Combine(temporaryRoot, "selected directory")).FullName;
            using var engine = StartEngine(
                environment:
                    new Dictionary<string, string?>
                    {
                        ["ChangeLens__Repositories__GitExecutable"] = GitFixtureExecutablePath,
                        ["CHANGELENS_GIT_FIXTURE_MODE"] = "sleep",
                    });

            await engine.StandardInput.WriteLineAsync(
                CreateOpenRequest("timeout-request", selectedPath));
            engine.StandardInput.Close();
            using var response = await ReadResponseAsync(engine, TimeSpan.FromSeconds(25));
            await WaitForExitAsync(engine);

            Assert.Equal("git.timedOut", FirstErrorCode(response));
            Assert.Equal(0, engine.ExitCode);
        }
        finally
        {
            DeleteTemporaryRoot(temporaryRoot);
        }
    }

    /// <summary>
    ///     Verifies one Engine process handles status, repository open, and status sequentially.
    /// </summary>
    [Fact]
    public async Task EngineProcessesStatusOpenStatusSequentially()
    {
        var temporaryRoot = CreateTemporaryRoot();

        try
        {
            var repositoryPath = InitializeRepository(temporaryRoot, "sequential repository");
            using var engine = StartEngine();

            await engine.StandardInput.WriteLineAsync(CreateStatusRequest("status-before"));
            await engine.StandardInput.WriteLineAsync(CreateOpenRequest("open-middle", repositoryPath));
            await engine.StandardInput.WriteLineAsync(CreateStatusRequest("status-after"));
            engine.StandardInput.Close();

            using var first = await ReadResponseAsync(engine);
            using var second = await ReadResponseAsync(engine);
            using var third = await ReadResponseAsync(engine);
            await WaitForExitAsync(engine);

            Assert.Equal("status-before", first.RootElement.GetProperty("requestId").GetString());
            Assert.Equal(JsonValueKind.Null, first.RootElement.GetProperty("result").ValueKind);
            AssertRepositoryEnvelope(second.RootElement, "open-middle");
            Assert.Equal("status-after", third.RootElement.GetProperty("requestId").GetString());
            Assert.Equal(JsonValueKind.Null, third.RootElement.GetProperty("result").ValueKind);
            Assert.Equal(0, engine.ExitCode);
        }
        finally
        {
            DeleteTemporaryRoot(temporaryRoot);
        }
    }

    /// <summary>
    ///     Verifies a valid repository open succeeds after recoverable malformed input.
    /// </summary>
    [Fact]
    public async Task EngineProcessesValidOpenAfterMalformedRepositoryInput()
    {
        var temporaryRoot = CreateTemporaryRoot();

        try
        {
            var repositoryPath = InitializeRepository(temporaryRoot, "recovery repository");
            using var engine = StartEngine();

            await engine.StandardInput.WriteLineAsync(
                """{"protocolVersion":1,"requestId":"invalid-open","action":"repositories.open","parameters":{"path":1}}""");
            await engine.StandardInput.WriteLineAsync(
                CreateOpenRequest("valid-open", repositoryPath));
            engine.StandardInput.Close();

            using var first = await ReadResponseAsync(engine);
            using var second = await ReadResponseAsync(engine);
            await WaitForExitAsync(engine);

            Assert.Equal("protocol.invalidRequest", FirstErrorCode(first));
            AssertRepositoryEnvelope(second.RootElement, "valid-open");
            Assert.Equal(0, engine.ExitCode);
        }
        finally
        {
            DeleteTemporaryRoot(temporaryRoot);
        }
    }

    /// <summary>
    ///     Verifies repository logs contain safe metadata while standard output contains only the response.
    /// </summary>
    [Fact]
    public async Task EngineLogsRepositoryOutcomeWithoutPathGitOutputOrStdoutPollution()
    {
        var temporaryRoot = CreateTemporaryRoot();
        var logDirectory = Path.Combine(temporaryRoot, "logs");

        try
        {
            var selectedPath = Directory.CreateDirectory(
                Path.Combine(temporaryRoot, "sensitive selected path")).FullName;
            using var engine = StartEngine(
                logDirectory,
                redirectStandardError: true);
            var request = CreateOpenRequest("repository-log-request", selectedPath);

            await engine.StandardInput.WriteLineAsync(request);
            engine.StandardInput.Close();

            var responseLine = await ReadResponseLineAsync(engine);
            await WaitForExitAsync(engine);
            var remainingOutput = await engine.StandardOutput.ReadToEndAsync(
                TestContext.Current.CancellationToken);
            var standardError = await engine.StandardError.ReadToEndAsync(
                TestContext.Current.CancellationToken);
            var logFile = Assert.Single(Directory.GetFiles(logDirectory, "changelens-engine-*.log"));
            var fileLog = await File.ReadAllTextAsync(
                logFile,
                TestContext.Current.CancellationToken);

            Assert.Equal(string.Empty, remainingOutput);
            AssertSafeRepositoryLog(standardError, selectedPath, request, responseLine);
            AssertSafeRepositoryLog(fileLog, selectedPath, request, responseLine);
        }
        finally
        {
            DeleteTemporaryRoot(temporaryRoot);
        }
    }

    private static string GitFixtureExecutablePath =>
        Path.Combine(
            AppContext.BaseDirectory,
            OperatingSystem.IsWindows()
                ? "ChangeLens.GitProcessFixture.exe"
                : "ChangeLens.GitProcessFixture");

    private static void AssertRepositoryEnvelope(JsonElement root, string requestId)
    {
        AssertExactProperties(root, "protocolVersion", "type", "requestId", "result");
        Assert.Equal(1, root.GetProperty("protocolVersion").GetInt32());
        Assert.Equal("result", root.GetProperty("type").GetString());
        Assert.Equal(requestId, root.GetProperty("requestId").GetString());
        AssertExactProperties(root.GetProperty("result"), "repository");
    }

    private static void AssertExactProperties(JsonElement element, params string[] expectedNames)
    {
        var actualNames = element.EnumerateObject()
            .Select(property => property.Name)
            .Order(StringComparer.Ordinal)
            .ToArray();
        Assert.Equal(expectedNames.Order(StringComparer.Ordinal), actualNames);
    }

    private static void AssertSafeRepositoryLog(
        string log,
        string selectedPath,
        string request,
        string response)
    {
        Assert.Contains("repository-log-request", log, StringComparison.Ordinal);
        Assert.Contains("repositories.open", log, StringComparison.Ordinal);
        Assert.Contains("repository.notGitRepository", log, StringComparison.Ordinal);
        Assert.Contains(" in ", log, StringComparison.Ordinal);
        Assert.Contains(" ms.", log, StringComparison.Ordinal);
        Assert.DoesNotContain(selectedPath, log, StringComparison.Ordinal);
        Assert.DoesNotContain("fatal: not a git repository", log, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain(request, log, StringComparison.Ordinal);
        Assert.DoesNotContain(response, log, StringComparison.Ordinal);
    }

    private static string FirstErrorCode(JsonDocument response) =>
        response.RootElement.GetProperty("errors")[0].GetProperty("code").GetString()!;

    private static string CreateOpenRequest(string requestId, string path) =>
        JsonSerializer.Serialize(
            new
            {
                protocolVersion = 1,
                requestId,
                action = "repositories.open",
                parameters = new { path },
            });

    private static string CreateStatusRequest(string requestId) =>
        JsonSerializer.Serialize(
            new
            {
                protocolVersion = 1,
                requestId,
                action = "engine.checkStatus",
            });

    private static async Task<string> ResolvePhysicalPathAsync(string path)
    {
        var result = await new PhysicalRepositoryPathResolver().ResolveAsync(
            path,
            TestContext.Current.CancellationToken);
        Assert.True(result.IsSuccess);
        return result.Data!;
    }

    private static string CreateTemporaryRoot()
    {
        var path = Path.Combine(
            Path.GetTempPath(),
            "ChangeLens.Engine.IntegrationTests",
            Guid.NewGuid().ToString("N"));
        return Directory.CreateDirectory(path).FullName;
    }

    private static string InitializeRepository(
        string temporaryRoot,
        string directoryName,
        bool bare = false,
        bool createCommit = true)
    {
        var repositoryPath = Path.Combine(temporaryRoot, directoryName);
        var arguments = new List<string> { "init" };
        if (bare)
        {
            arguments.Add("--bare");
        }
        else
        {
            arguments.Add("--initial-branch=main");
        }

        arguments.Add(repositoryPath);
        RunGitChecked(arguments);

        if (bare)
        {
            return repositoryPath;
        }

        RunGitChecked(["-C", repositoryPath, "config", "user.name", "ChangeLens Test"]);
        RunGitChecked(
            ["-C", repositoryPath, "config", "user.email", "changelens@example.invalid"]);
        RunGitChecked(["-C", repositoryPath, "config", "commit.gpgSign", "false"]);
        var hooksPath = Directory.CreateDirectory(
            Path.Combine(repositoryPath, ".change-lens-empty-hooks")).FullName;
        RunGitChecked(["-C", repositoryPath, "config", "core.hooksPath", hooksPath]);

        if (createCommit)
        {
            const string fileName = "fixture.txt";
            File.WriteAllText(
                Path.Combine(repositoryPath, fileName),
                "repository protocol fixture\n");
            RunGitChecked(["-C", repositoryPath, "add", "--", fileName]);
            RunGitChecked(
                [
                    "-C",
                    repositoryPath,
                    "commit",
                    "--quiet",
                    "--no-gpg-sign",
                    "-m",
                    "initial fixture",
                ]);
        }

        return repositoryPath;
    }

    private static string RunGitChecked(IReadOnlyList<string> arguments)
    {
        var startInfo = new ProcessStartInfo("git")
        {
            CreateNoWindow = true,
            RedirectStandardError = true,
            RedirectStandardOutput = true,
            UseShellExecute = false,
        };

        foreach (var argument in arguments)
        {
            startInfo.ArgumentList.Add(argument);
        }

        startInfo.Environment["GIT_OPTIONAL_LOCKS"] = "0";
        startInfo.Environment["GIT_TERMINAL_PROMPT"] = "0";
        startInfo.Environment["GCM_INTERACTIVE"] = "Never";
        startInfo.Environment["GIT_PAGER"] = "cat";
        startInfo.Environment["PAGER"] = "cat";
        startInfo.Environment["LC_ALL"] = "C";
        startInfo.Environment["LANG"] = "C";

        using var process = Process.Start(startInfo)
            ?? throw new InvalidOperationException("The controlled Git process could not be started.");
        var standardOutputTask = process.StandardOutput.ReadToEndAsync();
        var standardErrorTask = process.StandardError.ReadToEndAsync();

        if (!process.WaitForExit((int)TimeSpan.FromSeconds(15).TotalMilliseconds))
        {
            process.Kill(entireProcessTree: true);
            process.WaitForExit();
            throw new TimeoutException("The controlled Git process exceeded its allowed time.");
        }

        var standardOutput = standardOutputTask.GetAwaiter().GetResult();
        var standardError = standardErrorTask.GetAwaiter().GetResult();
        if (process.ExitCode != 0)
        {
            throw new InvalidOperationException(
                string.Create(
                    CultureInfo.InvariantCulture,
                    $"Controlled Git failed with exit code {process.ExitCode}: {standardError}"));
        }

        return standardOutput;
    }

    private static void DeleteTemporaryRoot(string path)
    {
        if (Directory.Exists(path))
        {
            Directory.Delete(path, recursive: true);
        }
    }

    private static async Task<JsonDocument> ReadResponseAsync(
        Process engine,
        TimeSpan? timeout = null) =>
        JsonDocument.Parse(await ReadResponseLineAsync(engine, timeout));

    private static async Task<string> ReadResponseLineAsync(
        Process engine,
        TimeSpan? timeout = null)
    {
        var responseLine = await engine.StandardOutput
            .ReadLineAsync(TestContext.Current.CancellationToken)
            .AsTask()
            .WaitAsync(
                timeout ?? TimeSpan.FromSeconds(10),
                TestContext.Current.CancellationToken);

        Assert.False(string.IsNullOrWhiteSpace(responseLine));
        return responseLine;
    }

    private static Task WaitForExitAsync(Process engine) =>
        engine.WaitForExitAsync(TestContext.Current.CancellationToken)
            .WaitAsync(TimeSpan.FromSeconds(10), TestContext.Current.CancellationToken);

    private static Process StartEngine(
        string? logDirectory = null,
        bool redirectStandardError = false,
        IReadOnlyDictionary<string, string?>? environment = null)
    {
        var engineProject = Path.Combine(
            RepositoryPaths.Root,
            "src",
            "engine",
            "ChangeLens.Engine",
            "ChangeLens.Engine.csproj");
        var startInfo = new ProcessStartInfo("dotnet")
        {
            RedirectStandardInput = true,
            RedirectStandardOutput = true,
            RedirectStandardError = redirectStandardError,
            UseShellExecute = false,
        };

        if (logDirectory is not null)
        {
            startInfo.Environment["ChangeLens__Logging__FileDirectory"] = logDirectory;
            startInfo.Environment["Serilog__MinimumLevel__Default"] = "Debug";
        }

        if (environment is not null)
        {
            foreach (var (name, value) in environment)
            {
                startInfo.Environment[name] = value;
            }
        }

        startInfo.ArgumentList.Add("run");
        startInfo.ArgumentList.Add("--no-build");
        startInfo.ArgumentList.Add("--project");
        startInfo.ArgumentList.Add(engineProject);

        return Process.Start(startInfo)
            ?? throw new InvalidOperationException("The ChangeLens engine process could not be started.");
    }
}
