using System.Diagnostics;
using System.Text;
using System.Text.Json;
using ChangeLens.Core.AnalysisRuns.Constants;
using ChangeLens.Core.Results.Models;
using ChangeLens.Engine.AnalysisRuns.Models;
using ChangeLens.Engine.IntegrationTests.Protocol.Support;
using ChangeLens.Engine.IntegrationTests.Support;
using ChangeLens.Engine.Protocol.Helpers;
using ChangeLens.Engine.Protocol.Services;
using Xunit;

namespace ChangeLens.Engine.IntegrationTests.Protocol;

/// <summary>
///     Verifies analysis poll responses and Engine resource use remain bounded at maximum accepted context size.
/// </summary>
public sealed class AnalysisCapacityProtocolTests
{
    private const int PollSummaryBudgetBytes = 48 * 1024;
    private const long MaximumEngineWorkingSetBytes = 512L * 1024 * 1024;
    private const long MaximumEngineWorkingSetGrowthBytes = 256L * 1024 * 1024;

    /// <summary>Asynchronously verifies maximum accepted change context produces a bounded terminal summary and working set.</summary>
    /// <returns>A task that represents the asynchronous operation.</returns>
    [Fact]
    public async Task MaximumBoundedPollSummaryStaysAtOrBelow48KiB()
    {
        using var repository = new ProtocolTemporaryGitRepository();
        repository.CommitFile("a.txt", "content");
        using var logDirectory = new TemporaryDirectory();
        await using var engine = await ProtocolTestEngine.StartAsync(logDirectory.DirectoryPath);
        var runningEngine = engine.Process;
        runningEngine.Refresh();
        var baselineWorkingSetBytes = runningEngine.WorkingSet64;
        var workingSetSampleLock = new object();
        var sampledPeakWorkingSetBytes = baselineWorkingSetBytes;
        using var workingSetSamplerCancellation = new CancellationTokenSource();
        var workingSetSampler = SampleWorkingSetAsync(TrySampleEngineWorkingSet, workingSetSamplerCancellation.Token);
        try
        {
            await engine.OpenRepositoryAsync(repository.Path);
            var freshnessToken = await engine.PrepareFreshnessTokenAsync(repository.Path, repository.DefaultTarget);
            using var startResponse = await engine.SendAsync(
                "analysis.start",
                "analysis-capacity-start",
                JsonSerializer.Serialize(
                    new
                    {
                        path = repository.Path,
                        target = repository.DefaultTarget,
                        freshnessToken,
                        changeContext = new string('a', 8192),
                    }));
            var startResult = ProtocolResponseAssertions.AssertResultEnvelope(startResponse, "analysis-capacity-start");
            ProtocolResponseAssertions.AssertExactProperties(startResult, "state", "runId", "requestedAt");
            Assert.Equal("accepted", startResult.GetProperty("state").GetString());
            var runId = startResult.GetProperty("runId").GetString()!;

            using var terminal = await engine.PollUntilTerminalAsync(runId, TimeSpan.FromSeconds(10));
            var terminalResult = terminal.RootElement.GetProperty("result");
            var encodedByteCount = Encoding.UTF8.GetByteCount(terminal.RootElement.GetRawText());
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
            Assert.True(
                encodedByteCount <= PollSummaryBudgetBytes,
                $"Expected the poll response to stay at or below 48 KiB but it was {encodedByteCount} bytes.");
            Assert.Equal("failed", terminalResult.GetProperty("state").GetString());
            var terminalSummary = terminalResult.GetProperty("terminal");
            ProtocolResponseAssertions.AssertExactProperties(
                terminalSummary,
                "kind",
                "terminalAt",
                "failureCode");
            Assert.Equal("failed", terminalSummary.GetProperty("kind").GetString());
            Assert.Equal("modelCompletion.notConfigured", terminalSummary.GetProperty("failureCode").GetString());
            Assert.Equal(JsonValueKind.Null, terminalResult.GetProperty("readingModel").ValueKind);
            Assert.Contains(
                terminalResult.GetProperty("facts").EnumerateArray(),
                fact => fact.GetProperty("kind").GetString() == "changedFilesCaptured");
            Assert.InRange(finalSampledPeakWorkingSetBytes, baselineWorkingSetBytes, MaximumEngineWorkingSetBytes);
            Assert.True(
                workingSetGrowthBytes <= MaximumEngineWorkingSetGrowthBytes,
                $"Measured {workingSetGrowthBytes} bytes from sampled peak {finalSampledPeakWorkingSetBytes} bytes and baseline " +
                $"{baselineWorkingSetBytes} bytes.");
            TestContext.Current.TestOutputHelper?.WriteLine(
                $"analysis poll capacity: responseBytes={encodedByteCount}; baselineWorkingSetBytes={baselineWorkingSetBytes}; " +
                $"sampledPeakWorkingSetBytes={finalSampledPeakWorkingSetBytes}; " +
                $"osPeakWorkingSetBytes={osPeakWorkingSetBytes?.ToString() ?? "unavailable"}; " +
                $"workingSetGrowthBytes={workingSetGrowthBytes}");
        }
        finally
        {
            workingSetSamplerCancellation.Cancel();
            await workingSetSampler;
        }

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

    /// <summary>
    ///     Verifies a maximum fact-bounded summary stays at or below the poll-summary budget with the real serializer.
    /// </summary>
    [Fact]
    public void FactBoundedSummaryStaysAtOrBelowPollSummaryBudget()
    {
        var summary = new AnalysisRunSummaryResult(
            "0198a1b2-3c4d-4e5f-8a9b-0123456789ab",
            "completed",
            new AnalysisRepositoryResult(
                "5298a1b2-3c4d-4e5f-8a9b-0123456789ab",
                "repo",
                "/repo",
                new string('0', 40)),
            new AnalysisComparisonResult("refs/heads/main", new string('1', 40), new string('2', 64)),
            1,
            null,
            null,
            null,
            false,
            [.. Enumerable.Range(0, 32).Select(index => new AnalysisFactResult(new string('k', 64), index, new string('d', 512)))],
            null,
            null,
            null,
            null,
            null);
        var response = ProtocolResponseFactory.FromResult("capacity-summary", Result.Success(summary));

        var byteCount = new EngineProtocolSerializer().GetSerializedUtf8ByteCount(response);

        Assert.True(byteCount.IsSuccess);
        Assert.True(
            byteCount.Data <= AnalysisRunLimits.PollSummaryMaxBytes,
            $"Expected at or below {AnalysisRunLimits.PollSummaryMaxBytes} bytes but the response was {byteCount.Data} bytes.");
    }

    /// <summary>Asynchronously samples the Engine working set until sampling stops or the Engine exits.</summary>
    /// <param name="trySample">Samples the Engine and returns <see langword="true" /> while it remains available.</param>
    /// <param name="cancellationToken">A token that stops background sampling without controlling the Engine process.</param>
    /// <returns>A task that represents the asynchronous operation.</returns>
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
}
