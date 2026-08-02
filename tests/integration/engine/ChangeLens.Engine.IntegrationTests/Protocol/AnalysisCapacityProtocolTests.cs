using System.Diagnostics;
using System.Text;
using System.Text.Json;
using ChangeLens.Engine.IntegrationTests.Protocol.Support;
using ChangeLens.Engine.IntegrationTests.Support;
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
            Assert.Equal("completed", terminalResult.GetProperty("state").GetString());
            ProtocolResponseAssertions.AssertExactProperties(
                terminalResult.GetProperty("terminal"),
                "kind",
                "terminalAt");
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
