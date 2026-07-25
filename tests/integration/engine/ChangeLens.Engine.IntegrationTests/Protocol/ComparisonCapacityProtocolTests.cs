using System.Diagnostics;
using System.Text;
using System.Text.Json;
using ChangeLens.Engine.IntegrationTests.Support;
using Xunit;

namespace ChangeLens.Engine.IntegrationTests.Protocol;

/// <summary>
///     Verifies measured comparison target pages remain bounded across a high-cardinality repository.
/// </summary>
public sealed class ComparisonCapacityProtocolTests
{
    private const int TargetPageBudgetBytes = 48 * 1024;
    private const long MaximumEngineWorkingSetBytes = 512L * 1024 * 1024;
    private const long MaximumEngineWorkingSetGrowthBytes = 256L * 1024 * 1024;

    /// <summary>
    ///     Asynchronously verifies high-cardinality target paging is complete, ordered, bounded, and change-aware.
    /// </summary>
    /// <returns>A task that represents the asynchronous operation.</returns>
    [Fact]
    public async Task EnginePagesHighCardinalityTargetsWithin48KiBAndRejectsChangedContinuation()
    {
        var root = Directory.CreateDirectory(Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"))).FullName;
        Process? engine = null;
        CancellationTokenSource? workingSetSamplerCancellation = null;
        Task? workingSetSampler = null;
        try
        {
            var repository = Path.Combine(root, "repository");
            Git(["init", "--initial-branch=main", repository]);
            Git(["-C", repository, "config", "user.name", "ChangeLens Test"]);
            Git(["-C", repository, "config", "user.email", "changelens@example.invalid"]);
            await File.WriteAllTextAsync(Path.Combine(repository, "fixture.txt"), "fixture\n", TestContext.Current.CancellationToken);
            Git(["-C", repository, "add", "--", "fixture.txt"]);
            Git(["-C", repository, "commit", "--quiet", "--no-gpg-sign", "-m", "fixture"]);
            var revision = Git(["-C", repository, "rev-parse", "HEAD"]).Trim();
            var expected = CreateRefFixtures(repository, revision);

            engine = StartEngine();
            engine.Refresh();
            var baselineWorkingSetBytes = engine.WorkingSet64;
            var workingSetSampleLock = new object();
            var sampledPeakWorkingSetBytes = baselineWorkingSetBytes;
            var runningEngine = engine;
            workingSetSamplerCancellation = new CancellationTokenSource();
            workingSetSampler = SampleWorkingSetAsync(TrySampleEngineWorkingSet, workingSetSamplerCancellation.Token);
            var emitted = new List<string>();
            string? after = null;
            string? token = null;
            var maximumResponseBytes = 0;
            var pageCount = 0;
            var unsupportedTargetCounts = new List<int>();
            var sawEscapedQuote = false;
            while (true)
            {
                object parameters = after is null
                    ? new { path = repository }
                    : new { path = repository, after, targetSetToken = token };
                await engine.StandardInput.WriteLineAsync(
                    Request("capacity", "comparisons.listTargets", parameters));
                var responseLine = await ReadResponse(engine);
                Assert.True(TrySampleEngineWorkingSet(), "The Engine exited while target pages were being sampled.");
                using var response = JsonDocument.Parse(responseLine);
                var responseBytes = Encoding.UTF8.GetByteCount(response.RootElement.GetRawText());
                maximumResponseBytes = Math.Max(maximumResponseBytes, responseBytes);
                pageCount++;
                sawEscapedQuote |= responseLine.Contains("\\\"", StringComparison.Ordinal) ||
                                   responseLine.Contains("\\u0022", StringComparison.Ordinal);
                Assert.True(responseBytes <= TargetPageBudgetBytes, $"Measured {responseBytes} bytes.");
                var result = response.RootElement.GetProperty("result");
                emitted.AddRange(result.GetProperty("targets").EnumerateArray().Select(target => target.GetProperty("fullName").GetString()!));
                unsupportedTargetCounts.Add(result.GetProperty("unsupportedTargetCount").GetInt32());
                after = result.GetProperty("nextCursor").GetString();
                token = result.GetProperty("targetSetToken").GetString();
                if (after is null)
                {
                    break;
                }
            }

            Assert.Equal(expected, emitted);
            Assert.Equal(emitted.Count, emitted.Distinct(StringComparer.Ordinal).Count());
            Assert.DoesNotContain("refs/heads/main", emitted);
            Assert.All(unsupportedTargetCounts, count => Assert.Equal(0, count));
            Assert.True(pageCount > 1);
            Assert.True(maximumResponseBytes > 0);
            Assert.True(sawEscapedQuote);
            File.WriteAllText(Path.Combine(repository, ".git", "refs", "heads", "changed-between-pages"), revision + "\n");
            await engine.StandardInput.WriteLineAsync(Request("changed", "comparisons.listTargets", new { path = repository, after = emitted[0], targetSetToken = token }));
            var changedResponse = await ReadResponse(engine);
            Assert.True(TrySampleEngineWorkingSet(), "The Engine exited while the changed-target response was being sampled.");
            using var changed = JsonDocument.Parse(changedResponse);
            Assert.Equal("comparison.targetsChanged", changed.RootElement.GetProperty("errors")[0].GetProperty("code").GetString());
            Assert.True(TrySampleEngineWorkingSet(), "The Engine exited before its final working-set sample.");
            long? osPeakWorkingSetBytes;
            long finalSampledPeakWorkingSetBytes;
            lock (workingSetSampleLock)
            {
                runningEngine.Refresh();
                var reportedOsPeakWorkingSetBytes = runningEngine.PeakWorkingSet64;
                osPeakWorkingSetBytes = reportedOsPeakWorkingSetBytes > 0
                    ? reportedOsPeakWorkingSetBytes
                    : null;
                if (osPeakWorkingSetBytes is long measuredOsPeakWorkingSetBytes)
                {
                    sampledPeakWorkingSetBytes = Math.Max(sampledPeakWorkingSetBytes, measuredOsPeakWorkingSetBytes);
                }

                finalSampledPeakWorkingSetBytes = sampledPeakWorkingSetBytes;
            }

            var workingSetGrowthBytes = Math.Max(0, finalSampledPeakWorkingSetBytes - baselineWorkingSetBytes);
            Assert.InRange(finalSampledPeakWorkingSetBytes, baselineWorkingSetBytes, MaximumEngineWorkingSetBytes);
            Assert.True(
                workingSetGrowthBytes <= MaximumEngineWorkingSetGrowthBytes,
                $"Measured {workingSetGrowthBytes} bytes from sampled peak {finalSampledPeakWorkingSetBytes} bytes and baseline {baselineWorkingSetBytes} bytes.");
            TestContext.Current.TestOutputHelper?.WriteLine(
                $"comparison target capacity: pages={pageCount}; maximumResponseBytes={maximumResponseBytes}; supported={emitted.Count}; unsupported=0; baselineWorkingSetBytes={baselineWorkingSetBytes}; sampledPeakWorkingSetBytes={finalSampledPeakWorkingSetBytes}; osPeakWorkingSetBytes={osPeakWorkingSetBytes?.ToString() ?? "unavailable"}; workingSetGrowthBytes={workingSetGrowthBytes}");

            bool TrySampleEngineWorkingSet()
            {
                lock (workingSetSampleLock)
                {
                    if (runningEngine.HasExited)
                    {
                        return false;
                    }

                    runningEngine.Refresh();
                    sampledPeakWorkingSetBytes = Math.Max(sampledPeakWorkingSetBytes, runningEngine.WorkingSet64);
                    return true;
                }
            }
        }
        finally
        {
            try
            {
                if (workingSetSamplerCancellation is not null)
                {
                    workingSetSamplerCancellation.Cancel();
                }

                if (workingSetSampler is not null)
                {
                    await workingSetSampler;
                }
            }
            finally
            {
                workingSetSamplerCancellation?.Dispose();
                await CloseEngineAsync(engine);
                Directory.Delete(root, recursive: true);
            }
        }
    }

    /// <summary>
    ///     Asynchronously samples the Engine working set until sampling stops or the Engine exits.
    /// </summary>
    /// <param name="trySample">
    ///     Samples the Engine and returns <see langword="true" /> while it remains available.
    /// </param>
    /// <param name="cancellationToken">
    ///     A token that stops background sampling without controlling the test or Engine process.
    /// </param>
    /// <returns>A task that represents the asynchronous sampling operation.</returns>
    private static async Task SampleWorkingSetAsync(Func<bool> trySample, CancellationToken cancellationToken)
    {
        try
        {
            while (!cancellationToken.IsCancellationRequested && trySample())
            {
                await Task.Delay(TimeSpan.FromMilliseconds(5), cancellationToken);
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
        }
    }

    private static IReadOnlyList<string> CreateRefFixtures(string repository, string revision)
    {
        var expected = new List<string>();
        for (var index = 0; index < 360; index++)
        {
            var name = $"{index:D4}-quote-unicode-λ-" + new string('x', 130);
            var local = Path.Combine(repository, ".git", "refs", "heads", name);
            var remote = Path.Combine(repository, ".git", "refs", "remotes", "origin", name);
            Directory.CreateDirectory(Path.GetDirectoryName(local)!);
            Directory.CreateDirectory(Path.GetDirectoryName(remote)!);
            File.WriteAllText(local, revision + "\n");
            File.WriteAllText(remote, revision + "\n");
            expected.Add("refs/heads/" + name);
            expected.Add("refs/remotes/origin/" + name);
        }

        const string quotedName = "json-quote-\"-unicode-λ";
        Git(["-C", repository, "update-ref", "refs/heads/" + quotedName, revision]);
        Git(["-C", repository, "update-ref", "refs/remotes/origin/" + quotedName, revision]);
        expected.Add("refs/heads/" + quotedName);
        expected.Add("refs/remotes/origin/" + quotedName);
        return expected
            .OrderBy(name => name.StartsWith("refs/remotes/", StringComparison.Ordinal))
            .ThenBy(name => name, StringComparer.Ordinal)
            .ToArray();
    }

    private static string Request(string requestId, string action, object parameters) => JsonSerializer.Serialize(new { protocolVersion = 1, requestId, action, parameters });

    private static string Git(IReadOnlyList<string> arguments)
    {
        var startInfo = new ProcessStartInfo("git") { RedirectStandardOutput = true, RedirectStandardError = true, UseShellExecute = false };
        foreach (var argument in arguments) startInfo.ArgumentList.Add(argument);
        using var process = Process.Start(startInfo) ?? throw new InvalidOperationException("Git did not start.");
        var output = process.StandardOutput.ReadToEnd();
        var error = process.StandardError.ReadToEnd();
        process.WaitForExit();
        Assert.True(process.ExitCode == 0, error);
        return output;
    }

    private static async Task<string> ReadResponse(Process engine) =>
        await engine.StandardOutput.ReadLineAsync(TestContext.Current.CancellationToken).AsTask().WaitAsync(TimeSpan.FromSeconds(35), TestContext.Current.CancellationToken)
        ?? throw new InvalidOperationException("The Engine returned no protocol response.");

    private static Process StartEngine()
    {
        var engineDll = Path.Combine(
            RepositoryPaths.Root,
            "src",
            "engine",
            "ChangeLens.Engine",
            "bin",
            "Debug",
            "net10.0",
            "ChangeLens.Engine.dll");
        Assert.True(File.Exists(engineDll), "The built Engine DLL must exist before the capacity test starts.");
        var startInfo = new ProcessStartInfo("dotnet") { RedirectStandardInput = true, RedirectStandardOutput = true, UseShellExecute = false };
        startInfo.ArgumentList.Add(engineDll);
        return Process.Start(startInfo) ?? throw new InvalidOperationException("The Engine did not start.");
    }

    private static async Task CloseEngineAsync(Process? engine)
    {
        if (engine is null)
        {
            return;
        }

        try
        {
            engine.StandardInput.Close();
            await engine.WaitForExitAsync(CancellationToken.None)
                .WaitAsync(TimeSpan.FromSeconds(10), CancellationToken.None);
        }
        catch (Exception exception) when (exception is TimeoutException or OperationCanceledException)
        {
            if (!engine.HasExited)
            {
                engine.Kill(entireProcessTree: true);
            }
            await engine.WaitForExitAsync(CancellationToken.None)
                .WaitAsync(TimeSpan.FromSeconds(10));
        }
        finally
        {
            engine.Dispose();
        }
    }
}
