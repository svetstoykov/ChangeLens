using System.Diagnostics;
using System.Globalization;
using System.Text.Json;
using ChangeLens.Engine.IntegrationTests.Support;
using Xunit;

namespace ChangeLens.Engine.IntegrationTests.Protocol;

/// <summary>
///     Verifies comparison actions through the real newline-delimited Engine process boundary.
/// </summary>
public sealed class ComparisonProtocolTests
{
    /// <summary>
    ///     Verifies list, prepare, freshness, recoverable errors, and later valid work in one Engine process.
    /// </summary>
    [Fact]
    public async Task EngineProcessesComparisonJourneySequentially()
    {
        var temporaryRoot = CreateTemporaryRoot();

        try
        {
            var repositoryPath = InitializeReadyRepository(temporaryRoot);
            using var engine = StartEngine();

            await engine.StandardInput.WriteLineAsync(
                CreateRequest(
                    "list-first",
                    "comparisons.listTargets",
                    new { path = repositoryPath }));
            using var list = await ReadResponseAsync(engine);
            AssertResultEnvelope(list.RootElement, "list-first");
            var page = list.RootElement.GetProperty("result");
            AssertExactProperties(
                page,
                "targets",
                "suggestedTarget",
                "nextCursor",
                "targetSetToken",
                "unsupportedTargetCount");
            Assert.True(list.RootElement.GetRawText().Length < 64 * 1024);
            var targets = page.GetProperty("targets").EnumerateArray().ToArray();
            Assert.Contains(
                targets,
                target => target.GetProperty("kind").GetString() == "local" &&
                          target.GetProperty("fullName").GetString() == "refs/heads/topic");
            Assert.Contains(
                targets,
                target => target.GetProperty("kind").GetString() == "remoteTracking" &&
                          target.GetProperty("fullName").GetString() ==
                          "refs/remotes/origin/main");
            Assert.Equal(
                "refs/remotes/origin/main",
                page.GetProperty("suggestedTarget").GetProperty("fullName").GetString());

            await engine.StandardInput.WriteLineAsync(
                CreateRequest(
                    "prepare-ready",
                    "comparisons.prepare",
                    new { path = repositoryPath, target = "refs/heads/topic" }));
            using var prepare = await ReadResponseAsync(engine);
            AssertResultEnvelope(prepare.RootElement, "prepare-ready");
            var prepared = prepare.RootElement.GetProperty("result");
            AssertExactProperties(
                prepared,
                "repository",
                "target",
                "mergeBaseRevision",
                "currentWorkCommitCount",
                "targetOnlyCommitCount",
                "changedFileTotal",
                "uncommittedFileTotal",
                "stagedFileCount",
                "unstagedFileCount",
                "untrackedFileCount",
                "readiness",
                "freshnessToken");
            Assert.Equal("ready", prepared.GetProperty("readiness").GetProperty("state").GetString());
            Assert.Equal(1, prepared.GetProperty("currentWorkCommitCount").GetInt32());
            Assert.Equal(0, prepared.GetProperty("targetOnlyCommitCount").GetInt32());
            Assert.Equal(1, prepared.GetProperty("changedFileTotal").GetInt32());
            var freshnessToken = prepared.GetProperty("freshnessToken").GetString()!;

            await engine.StandardInput.WriteLineAsync(
                CreateRequest(
                    "fresh-current",
                    "comparisons.checkFreshness",
                    new
                    {
                        path = repositoryPath,
                        target = "refs/heads/topic",
                        freshnessToken,
                    }));
            using var freshness = await ReadResponseAsync(engine);
            AssertResultEnvelope(freshness.RootElement, "fresh-current");
            Assert.Equal(
                "current",
                freshness.RootElement.GetProperty("result").GetProperty("state").GetString());

            await engine.StandardInput.WriteLineAsync(
                """{"protocolVersion":1,"requestId":"malformed","action":"comparisons.prepare","parameters":{"path":"/repo"}}""");
            using var malformed = await ReadResponseAsync(engine);
            Assert.Equal("malformed", malformed.RootElement.GetProperty("requestId").GetString());
            Assert.Equal("protocol.invalidRequest", FirstErrorCode(malformed));

            await engine.StandardInput.WriteLineAsync(
                CreateRequest(
                    "recoverable",
                    "comparisons.checkFreshness",
                    new
                    {
                        path = repositoryPath,
                        target = "refs/heads/topic",
                        freshnessToken = "invalid",
                    }));
            using var recoverable = await ReadResponseAsync(engine);
            Assert.Equal("comparison.invalidFreshnessToken", FirstErrorCode(recoverable));

            await engine.StandardInput.WriteLineAsync(
                CreateRequest(
                    "list-after-errors",
                    "comparisons.listTargets",
                    new { path = repositoryPath, query = "topic" }));
            engine.StandardInput.Close();
            using var final = await ReadResponseAsync(engine);
            await WaitForExitAsync(engine);

            AssertResultEnvelope(final.RootElement, "list-after-errors");
            Assert.Single(final.RootElement.GetProperty("result").GetProperty("targets").EnumerateArray());
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
    ///     Verifies empty preparation and a later working-tree change map to the tagged freshness states.
    /// </summary>
    [Fact]
    public async Task EngineMapsEmptyAndStaleComparisonStates()
    {
        var temporaryRoot = CreateTemporaryRoot();

        try
        {
            var repositoryPath = InitializeRepository(temporaryRoot, "empty repository");
            RunGitChecked(["-C", repositoryPath, "branch", "same"]);
            using var engine = StartEngine();

            await engine.StandardInput.WriteLineAsync(
                CreateRequest(
                    "prepare-empty",
                    "comparisons.prepare",
                    new { path = repositoryPath, target = "refs/heads/same" }));
            using var preparedResponse = await ReadResponseAsync(engine);
            var prepared = preparedResponse.RootElement.GetProperty("result");
            Assert.Equal("empty", prepared.GetProperty("readiness").GetProperty("state").GetString());
            Assert.Equal(0, prepared.GetProperty("changedFileTotal").GetInt32());
            var freshnessToken = prepared.GetProperty("freshnessToken").GetString()!;

            await File.WriteAllTextAsync(
                Path.Combine(repositoryPath, "untracked.txt"),
                "changed after preparation\n",
                TestContext.Current.CancellationToken);
            await engine.StandardInput.WriteLineAsync(
                CreateRequest(
                    "fresh-stale",
                    "comparisons.checkFreshness",
                    new
                    {
                        path = repositoryPath,
                        target = "refs/heads/same",
                        freshnessToken,
                    }));
            engine.StandardInput.Close();
            using var freshness = await ReadResponseAsync(engine);
            await WaitForExitAsync(engine);

            Assert.Equal(
                "stale",
                freshness.RootElement.GetProperty("result").GetProperty("state").GetString());
            Assert.Equal(0, engine.ExitCode);
        }
        finally
        {
            DeleteTemporaryRoot(temporaryRoot);
        }
    }

    /// <summary>
    ///     Verifies an unresolved working-tree merge maps to the strict conflicts readiness variant.
    /// </summary>
    [Fact]
    public async Task EngineMapsConflictsReadiness()
    {
        var temporaryRoot = CreateTemporaryRoot();

        try
        {
            var repositoryPath = InitializeRepository(temporaryRoot, "conflict repository");
            RunGitChecked(["-C", repositoryPath, "branch", "conflict-target"]);
            RunGitChecked(["-C", repositoryPath, "checkout", "--quiet", "conflict-target"]);
            await File.WriteAllTextAsync(
                Path.Combine(repositoryPath, "fixture.txt"),
                "target\n",
                TestContext.Current.CancellationToken);
            CommitAll(repositoryPath, "target change");
            RunGitChecked(["-C", repositoryPath, "checkout", "--quiet", "main"]);
            await File.WriteAllTextAsync(
                Path.Combine(repositoryPath, "fixture.txt"),
                "current\n",
                TestContext.Current.CancellationToken);
            CommitAll(repositoryPath, "current change");
            RunGit(
                ["-C", repositoryPath, "merge", "--no-edit", "conflict-target"],
                expectedExitCode: 1);
            using var engine = StartEngine();

            await engine.StandardInput.WriteLineAsync(
                CreateRequest(
                    "prepare-conflicts",
                    "comparisons.prepare",
                    new { path = repositoryPath, target = "refs/heads/conflict-target" }));
            engine.StandardInput.Close();
            using var response = await ReadResponseAsync(engine);
            await WaitForExitAsync(engine);

            var readiness = response.RootElement.GetProperty("result").GetProperty("readiness");
            AssertExactProperties(readiness, "state", "conflictedFileCount");
            Assert.Equal("conflicts", readiness.GetProperty("state").GetString());
            Assert.Equal(1, readiness.GetProperty("conflictedFileCount").GetInt32());
            Assert.Equal(0, engine.ExitCode);
        }
        finally
        {
            DeleteTemporaryRoot(temporaryRoot);
        }
    }

    /// <summary>
    ///     Verifies comparison validation errors use their stable codes without terminating the Engine.
    /// </summary>
    [Fact]
    public async Task EngineReturnsStableComparisonValidationCodes()
    {
        var overlongQuery = new string('q', 257);
        using var engine = StartEngine();
        var requests = new[]
        {
            CreateRequest(
                "invalid-query",
                "comparisons.listTargets",
                new { path = "/unused", query = overlongQuery }),
            CreateRequest(
                "invalid-page",
                "comparisons.listTargets",
                new { path = "/unused", after = "refs/heads/topic" }),
            CreateRequest(
                "invalid-target",
                "comparisons.prepare",
                new { path = "/unused", target = "topic" }),
            CreateRequest(
                "invalid-token",
                "comparisons.checkFreshness",
                new
                {
                    path = "/unused",
                    target = "refs/heads/topic",
                    freshnessToken = "short",
                }),
        };

        foreach (var request in requests)
        {
            await engine.StandardInput.WriteLineAsync(request);
        }

        engine.StandardInput.Close();
        var expectedCodes = new[]
        {
            "comparison.invalidTargetQuery",
            "comparison.invalidTargetPage",
            "comparison.targetInvalid",
            "comparison.invalidFreshnessToken",
        };

        foreach (var expectedCode in expectedCodes)
        {
            using var response = await ReadResponseAsync(engine);
            Assert.Equal(expectedCode, FirstErrorCode(response));
        }

        await WaitForExitAsync(engine);
        Assert.Equal(0, engine.ExitCode);
    }

    /// <summary>
    ///     Verifies a continuation rejects a target set changed after the first page.
    /// </summary>
    [Fact]
    public async Task EngineReturnsTargetsChangedAfterRepositoryMutation()
    {
        var temporaryRoot = CreateTemporaryRoot();

        try
        {
            var repositoryPath = InitializeReadyRepository(temporaryRoot);
            using var engine = StartEngine();

            await engine.StandardInput.WriteLineAsync(
                CreateRequest(
                    "targets-before-change",
                    "comparisons.listTargets",
                    new { path = repositoryPath }));
            using var firstPage = await ReadResponseAsync(engine);
            var result = firstPage.RootElement.GetProperty("result");
            var cursor = result.GetProperty("targets")[0].GetProperty("fullName").GetString()!;
            var token = result.GetProperty("targetSetToken").GetString()!;
            RunGitChecked(["-C", repositoryPath, "branch", "added-after-page"]);

            await engine.StandardInput.WriteLineAsync(
                CreateRequest(
                    "targets-after-change",
                    "comparisons.listTargets",
                    new
                    {
                        path = repositoryPath,
                        after = cursor,
                        targetSetToken = token,
                    }));
            engine.StandardInput.Close();
            using var response = await ReadResponseAsync(engine);
            await WaitForExitAsync(engine);

            Assert.Equal("comparison.targetsChanged", FirstErrorCode(response));
            Assert.Equal(0, engine.ExitCode);
        }
        finally
        {
            DeleteTemporaryRoot(temporaryRoot);
        }
    }

    /// <summary>
    ///     Verifies a target deleted after discovery maps to the stable unavailable error.
    /// </summary>
    [Fact]
    public async Task EngineReturnsTargetUnavailableWhenRefDisappearsAfterDiscovery()
    {
        var temporaryRoot = CreateTemporaryRoot();

        try
        {
            var repositoryPath = InitializeRepository(temporaryRoot, "vanishing target repository");
            RunGitChecked(["-C", repositoryPath, "branch", "vanishing"]);
            using var engine = StartEngine(
                environment:
                    new Dictionary<string, string?>
                    {
                        ["ChangeLens__Repositories__GitExecutable"] = GitFixtureExecutablePath,
                        ["CHANGELENS_GIT_FIXTURE_MODE"] = "proxy-delete-target-after-check",
                        ["CHANGELENS_GIT_FIXTURE_REPOSITORY"] = repositoryPath,
                        ["CHANGELENS_GIT_FIXTURE_TARGET"] = "refs/heads/vanishing",
                    });

            await engine.StandardInput.WriteLineAsync(
                CreateRequest(
                    "target-unavailable",
                    "comparisons.prepare",
                    new { path = repositoryPath, target = "refs/heads/vanishing" }));
            engine.StandardInput.Close();
            using var response = await ReadResponseAsync(engine);
            await WaitForExitAsync(engine);

            Assert.Equal("comparison.targetUnavailable", FirstErrorCode(response));
            Assert.Equal(0, engine.ExitCode);
        }
        finally
        {
            DeleteTemporaryRoot(temporaryRoot);
        }
    }

    /// <summary>
    ///     Verifies a target with an independent root maps to the stable unrelated-history error.
    /// </summary>
    [Fact]
    public async Task EngineReturnsUnrelatedHistoryForIndependentRoot()
    {
        var temporaryRoot = CreateTemporaryRoot();

        try
        {
            var repositoryPath = InitializeRepository(temporaryRoot, "unrelated repository");
            var tree = RunGitChecked(
                ["-C", repositoryPath, "rev-parse", "HEAD^{tree}"]).Trim();
            var unrelated = RunGitChecked(
                ["-C", repositoryPath, "commit-tree", tree, "-m", "unrelated root"]).Trim();
            RunGitChecked(
                ["-C", repositoryPath, "update-ref", "refs/heads/unrelated", unrelated]);
            using var engine = StartEngine();

            await engine.StandardInput.WriteLineAsync(
                CreateRequest(
                    "unrelated-history",
                    "comparisons.prepare",
                    new { path = repositoryPath, target = "refs/heads/unrelated" }));
            engine.StandardInput.Close();
            using var response = await ReadResponseAsync(engine);
            await WaitForExitAsync(engine);

            Assert.Equal("comparison.unrelatedHistory", FirstErrorCode(response));
            Assert.Equal(0, engine.ExitCode);
        }
        finally
        {
            DeleteTemporaryRoot(temporaryRoot);
        }
    }

    /// <summary>
    ///     Verifies criss-cross histories with two best bases map to the stable ambiguous-base error.
    /// </summary>
    [Fact]
    public async Task EngineReturnsAmbiguousBaseForCrissCrossHistory()
    {
        var temporaryRoot = CreateTemporaryRoot();

        try
        {
            var repositoryPath = InitializeRepository(temporaryRoot, "ambiguous repository");
            var root = RunGitChecked(["-C", repositoryPath, "rev-parse", "HEAD"]).Trim();
            var tree = RunGitChecked(
                ["-C", repositoryPath, "rev-parse", "HEAD^{tree}"]).Trim();
            var firstLeft = RunGitChecked(
                ["-C", repositoryPath, "commit-tree", tree, "-p", root, "-m", "first left"]).Trim();
            var firstRight = RunGitChecked(
                ["-C", repositoryPath, "commit-tree", tree, "-p", root, "-m", "first right"]).Trim();
            var current = RunGitChecked(
                [
                    "-C",
                    repositoryPath,
                    "commit-tree",
                    tree,
                    "-p",
                    firstLeft,
                    "-p",
                    firstRight,
                    "-m",
                    "current merge",
                ]).Trim();
            var target = RunGitChecked(
                [
                    "-C",
                    repositoryPath,
                    "commit-tree",
                    tree,
                    "-p",
                    firstRight,
                    "-p",
                    firstLeft,
                    "-m",
                    "target merge",
                ]).Trim();
            RunGitChecked(["-C", repositoryPath, "update-ref", "refs/heads/main", current]);
            RunGitChecked(["-C", repositoryPath, "update-ref", "refs/heads/ambiguous", target]);
            RunGitChecked(["-C", repositoryPath, "reset", "--hard", "--quiet", "main"]);
            using var engine = StartEngine();

            await engine.StandardInput.WriteLineAsync(
                CreateRequest(
                    "ambiguous-base",
                    "comparisons.prepare",
                    new { path = repositoryPath, target = "refs/heads/ambiguous" }));
            engine.StandardInput.Close();
            using var response = await ReadResponseAsync(engine);
            await WaitForExitAsync(engine);

            Assert.Equal("comparison.ambiguousBase", FirstErrorCode(response));
            Assert.Equal(0, engine.ExitCode);
        }
        finally
        {
            DeleteTemporaryRoot(temporaryRoot);
        }
    }

    /// <summary>
    ///     Verifies a working-tree mutation during preparation maps to the stable conflict error.
    /// </summary>
    [Fact]
    public async Task EngineReturnsChangedDuringPreparationForMidActionMutation()
    {
        var temporaryRoot = CreateTemporaryRoot();

        try
        {
            var repositoryPath = InitializeRepository(temporaryRoot, "changing repository");
            RunGitChecked(["-C", repositoryPath, "branch", "same"]);
            var stateFile = Path.Combine(temporaryRoot, "status-count.txt");
            var mutationFile = Path.Combine(repositoryPath, "changed-during-preparation.txt");
            using var engine = StartEngine(
                environment:
                    new Dictionary<string, string?>
                    {
                        ["ChangeLens__Repositories__GitExecutable"] = GitFixtureExecutablePath,
                        ["CHANGELENS_GIT_FIXTURE_MODE"] = "proxy-change-on-second-status",
                        ["CHANGELENS_GIT_FIXTURE_STATE_FILE"] = stateFile,
                        ["CHANGELENS_GIT_FIXTURE_MUTATION_FILE"] = mutationFile,
                    });

            await engine.StandardInput.WriteLineAsync(
                CreateRequest(
                    "changed-during-preparation",
                    "comparisons.prepare",
                    new { path = repositoryPath, target = "refs/heads/same" }));
            engine.StandardInput.Close();
            using var response = await ReadResponseAsync(engine);
            await WaitForExitAsync(engine);

            Assert.Equal("comparison.changedDuringPreparation", FirstErrorCode(response));
            Assert.True(File.Exists(mutationFile));
            Assert.Equal(0, engine.ExitCode);
        }
        finally
        {
            DeleteTemporaryRoot(temporaryRoot);
        }
    }

    /// <summary>
    ///     Verifies bounded Git failures map to comparison-owned stable errors.
    /// </summary>
    /// <param name="fixtureMode">The controlled Git process behavior.</param>
    /// <param name="expectedCode">The expected stable comparison code.</param>
    [Theory]
    [InlineData("large-stdout", "comparison.tooLarge")]
    [InlineData("invalid-utf8", "comparison.inspectionFailed")]
    [InlineData("sleep", "comparison.timedOut")]
    public async Task EngineMapsBoundedGitFailuresToComparisonErrors(
        string fixtureMode,
        string expectedCode)
    {
        var temporaryRoot = CreateTemporaryRoot();

        try
        {
            var selectedPath = Directory.CreateDirectory(
                Path.Combine(temporaryRoot, "selected")).FullName;
            using var engine = StartEngine(
                environment:
                    new Dictionary<string, string?>
                    {
                        ["ChangeLens__Repositories__GitExecutable"] = GitFixtureExecutablePath,
                        ["CHANGELENS_GIT_FIXTURE_MODE"] = fixtureMode,
                    });

            await engine.StandardInput.WriteLineAsync(
                CreateRequest(
                    "bounded-failure",
                    "comparisons.listTargets",
                    new { path = selectedPath }));
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
    ///     Verifies comparison logs exclude caller parameters, transient context, and protocol payloads.
    /// </summary>
    [Fact]
    public async Task EngineLogsComparisonOutcomeWithoutSensitiveContextOrStdoutPollution()
    {
        var temporaryRoot = CreateTemporaryRoot();
        var logDirectory = Path.Combine(temporaryRoot, "logs");
        const string sensitivePath = "/sensitive/comparison/path";
        const string sensitiveQuery = "sensitive-query";
        const string sensitiveTarget = "refs/heads/sensitive-target";
        const string sensitiveToken = "sensitive-token";
        const string sensitiveContext = "private Change context";

        try
        {
            using var engine = StartEngine(logDirectory, redirectStandardError: true);
            var listRequest = CreateRequest(
                "comparison-list-log-request",
                "comparisons.listTargets",
                new
                {
                    path = sensitivePath,
                    query = sensitiveQuery + new string('q', 257),
                });
            var freshnessRequest = CreateRequest(
                "comparison-log-request",
                "comparisons.checkFreshness",
                new
                {
                    path = sensitivePath,
                    target = sensitiveTarget,
                    freshnessToken = sensitiveToken,
                });

            await engine.StandardInput.WriteLineAsync(listRequest);
            await engine.StandardInput.WriteLineAsync(freshnessRequest);
            engine.StandardInput.Close();
            var listResponseLine = await ReadResponseLineAsync(engine);
            var freshnessResponseLine = await ReadResponseLineAsync(engine);
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
            AssertSafeComparisonLog(
                standardError,
                listRequest,
                listResponseLine,
                freshnessRequest,
                freshnessResponseLine,
                sensitivePath,
                sensitiveQuery,
                sensitiveTarget,
                sensitiveToken,
                sensitiveContext);
            AssertSafeComparisonLog(
                fileLog,
                listRequest,
                listResponseLine,
                freshnessRequest,
                freshnessResponseLine,
                sensitivePath,
                sensitiveQuery,
                sensitiveTarget,
                sensitiveToken,
                sensitiveContext);
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

    private static string InitializeReadyRepository(string temporaryRoot)
    {
        var repositoryPath = InitializeRepository(temporaryRoot, "ready repository");
        RunGitChecked(["-C", repositoryPath, "branch", "topic"]);
        RunGitChecked(
            ["-C", repositoryPath, "update-ref", "refs/remotes/origin/main", "HEAD"]);
        RunGitChecked(
            [
                "-C",
                repositoryPath,
                "symbolic-ref",
                "refs/remotes/origin/HEAD",
                "refs/remotes/origin/main",
            ]);
        RunGitChecked(["-C", repositoryPath, "config", "branch.main.remote", "origin"]);
        RunGitChecked(["-C", repositoryPath, "config", "branch.main.merge", "refs/heads/main"]);
        File.WriteAllText(Path.Combine(repositoryPath, "main.txt"), "current work\n");
        CommitAll(repositoryPath, "current work");
        return repositoryPath;
    }

    private static string InitializeRepository(string temporaryRoot, string directoryName)
    {
        var repositoryPath = Path.Combine(temporaryRoot, directoryName);
        RunGitChecked(["init", "--initial-branch=main", repositoryPath]);
        RunGitChecked(["-C", repositoryPath, "config", "user.name", "ChangeLens Test"]);
        RunGitChecked(
            ["-C", repositoryPath, "config", "user.email", "changelens@example.invalid"]);
        RunGitChecked(["-C", repositoryPath, "config", "commit.gpgSign", "false"]);
        var hooksPath = Directory.CreateDirectory(
            Path.Combine(repositoryPath, ".change-lens-empty-hooks")).FullName;
        RunGitChecked(["-C", repositoryPath, "config", "core.hooksPath", hooksPath]);
        File.WriteAllText(Path.Combine(repositoryPath, "fixture.txt"), "baseline\n");
        CommitAll(repositoryPath, "initial fixture");
        return repositoryPath;
    }

    private static void CommitAll(string repositoryPath, string message)
    {
        RunGitChecked(["-C", repositoryPath, "add", "--all"]);
        RunGitChecked(
            [
                "-C",
                repositoryPath,
                "commit",
                "--quiet",
                "--no-gpg-sign",
                "-m",
                message,
            ]);
    }

    private static string CreateRequest(string requestId, string action, object parameters) =>
        JsonSerializer.Serialize(
            new
            {
                protocolVersion = 1,
                requestId,
                action,
                parameters,
            });

    private static void AssertResultEnvelope(JsonElement root, string requestId)
    {
        AssertExactProperties(root, "protocolVersion", "type", "requestId", "result");
        Assert.Equal(1, root.GetProperty("protocolVersion").GetInt32());
        Assert.Equal("result", root.GetProperty("type").GetString());
        Assert.Equal(requestId, root.GetProperty("requestId").GetString());
    }

    private static void AssertExactProperties(JsonElement element, params string[] expectedNames)
    {
        var actualNames = element.EnumerateObject()
            .Select(property => property.Name)
            .Order(StringComparer.Ordinal)
            .ToArray();
        Assert.Equal(expectedNames.Order(StringComparer.Ordinal), actualNames);
    }

    private static void AssertSafeComparisonLog(
        string log,
        string listRequest,
        string listResponse,
        string freshnessRequest,
        string freshnessResponse,
        params string[] sensitiveValues)
    {
        Assert.Contains("comparison-list-log-request", log, StringComparison.Ordinal);
        Assert.Contains("comparisons.listTargets", log, StringComparison.Ordinal);
        Assert.Contains("comparison.invalidTargetQuery", log, StringComparison.Ordinal);
        Assert.Contains("comparison-log-request", log, StringComparison.Ordinal);
        Assert.Contains("comparisons.checkFreshness", log, StringComparison.Ordinal);
        Assert.Contains("comparison.invalidFreshnessToken", log, StringComparison.Ordinal);
        Assert.DoesNotContain(listRequest, log, StringComparison.Ordinal);
        Assert.DoesNotContain(listResponse, log, StringComparison.Ordinal);
        Assert.DoesNotContain(freshnessRequest, log, StringComparison.Ordinal);
        Assert.DoesNotContain(freshnessResponse, log, StringComparison.Ordinal);

        foreach (var value in sensitiveValues)
        {
            Assert.DoesNotContain(value, log, StringComparison.Ordinal);
        }
    }

    private static string FirstErrorCode(JsonDocument response) =>
        response.RootElement.GetProperty("errors")[0].GetProperty("code").GetString()!;

    private static string CreateTemporaryRoot()
    {
        var path = Path.Combine(
            Path.GetTempPath(),
            "ChangeLens.Engine.IntegrationTests",
            Guid.NewGuid().ToString("N"));
        return Directory.CreateDirectory(path).FullName;
    }

    private static string RunGitChecked(IReadOnlyList<string> arguments) =>
        RunGit(arguments, expectedExitCode: 0);

    private static string RunGit(IReadOnlyList<string> arguments, int expectedExitCode)
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
        if (process.ExitCode != expectedExitCode)
        {
            throw new InvalidOperationException(
                string.Create(
                    CultureInfo.InvariantCulture,
                    $"Controlled Git exited with {process.ExitCode}: {standardError}"));
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

    private static async Task<JsonDocument> ReadResponseAsync(Process engine) =>
        JsonDocument.Parse(await ReadResponseLineAsync(engine));

    private static async Task<string> ReadResponseLineAsync(Process engine)
    {
        var responseLine = await engine.StandardOutput
            .ReadLineAsync(TestContext.Current.CancellationToken)
            .AsTask()
            .WaitAsync(TimeSpan.FromSeconds(35), TestContext.Current.CancellationToken);

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
