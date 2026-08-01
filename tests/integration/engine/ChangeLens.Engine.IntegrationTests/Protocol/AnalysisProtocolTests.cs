using System.Text.Json;
using ChangeLens.Engine.IntegrationTests.Protocol.Support;
using ChangeLens.Engine.IntegrationTests.Support;
using Xunit;

namespace ChangeLens.Engine.IntegrationTests.Protocol;

/// <summary>
///     Verifies analysis orchestration through the real newline-delimited Engine process boundary.
/// </summary>
public sealed class AnalysisProtocolTests
{
    /// <summary>Asynchronously verifies an accepted run can be observed through terminal completion.</summary>
    /// <returns>A task that represents the asynchronous operation.</returns>
    [Fact]
    public async Task StartPollAndObserveTerminalCompletionThroughTheRealHost()
    {
        using var repository = new ProtocolTemporaryGitRepository();
        repository.CommitFile("a.txt", "content");
        using var logDirectory = new TemporaryDirectory();
        await using var engine = await ProtocolTestEngine.StartAsync(logDirectory.DirectoryPath);
        await engine.OpenRepositoryAsync(repository.Path);
        var freshnessToken = await engine.PrepareFreshnessTokenAsync(repository.Path, repository.DefaultTarget);

        using var startResponse = await engine.SendAsync(
            "analysis.start",
            "analysis-start-1",
            JsonSerializer.Serialize(
                new
                {
                    path = repository.Path,
                    target = repository.DefaultTarget,
                    freshnessToken,
                }));
        var startResult = ProtocolResponseAssertions.AssertResultEnvelope(startResponse, "analysis-start-1");
        ProtocolResponseAssertions.AssertExactProperties(startResult, "state", "runId", "requestedAt");
        Assert.Equal("accepted", startResult.GetProperty("state").GetString());
        var runId = startResult.GetProperty("runId").GetString()!;

        using var terminal = await engine.PollUntilTerminalAsync(runId, TimeSpan.FromSeconds(10));

        var terminalResult = terminal.RootElement.GetProperty("result");
        Assert.Equal("completed", terminalResult.GetProperty("state").GetString());
        ProtocolResponseAssertions.AssertExactProperties(terminalResult.GetProperty("terminal"), "kind", "terminalAt");
    }

    /// <summary>Asynchronously verifies one of two starts for the same repository is rejected with the accepted run identifier.</summary>
    /// <returns>A task that represents the asynchronous operation.</returns>
    [Fact]
    public async Task RacingStartForSameRepositoryReturnsRejectedActiveWithTheAcceptedRunId()
    {
        using var repository = new ProtocolTemporaryGitRepository();
        repository.CommitFile("a.txt", "content");
        using var logDirectory = new TemporaryDirectory();
        await using var engine = await ProtocolTestEngine.StartAsync(logDirectory.DirectoryPath, blockPipelineUntilReleased: true);
        await engine.OpenRepositoryAsync(repository.Path);
        var freshnessToken = await engine.PrepareFreshnessTokenAsync(repository.Path, repository.DefaultTarget);
        var parameters = JsonSerializer.Serialize(
            new
            {
                path = repository.Path,
                target = repository.DefaultTarget,
                freshnessToken,
            });

        using var first = await engine.SendAsync("analysis.start", "analysis-start-a", parameters);
        await engine.WaitUntilPipelineBlockedAsync(TimeSpan.FromSeconds(10));
        using var second = await engine.SendAsync("analysis.start", "analysis-start-b", parameters);

        var firstResult = ProtocolResponseAssertions.AssertResultEnvelope(first, "analysis-start-a");
        var secondResult = ProtocolResponseAssertions.AssertResultEnvelope(second, "analysis-start-b");
        var responses = new[] { firstResult, secondResult };
        var accepted = Assert.Single(responses, result => result.GetProperty("state").GetString() == "accepted");
        var rejected = Assert.Single(responses, result => result.GetProperty("state").GetString() == "rejectedActive");
        ProtocolResponseAssertions.AssertExactProperties(accepted, "state", "runId", "requestedAt");
        ProtocolResponseAssertions.AssertExactProperties(rejected, "state", "activeRunId");
        Assert.Equal(accepted.GetProperty("runId").GetString(), rejected.GetProperty("activeRunId").GetString());
        engine.ReleaseBlockedPipeline();
        using var terminal = await engine.PollUntilTerminalAsync(accepted.GetProperty("runId").GetString()!, TimeSpan.FromSeconds(10));
    }

    /// <summary>Asynchronously verifies stale comparison facts are rejected without allocating a run.</summary>
    /// <returns>A task that represents the asynchronous operation.</returns>
    [Fact]
    public async Task StaleComparisonAtAcceptanceReturnsRejectedStaleWithoutARun()
    {
        using var repository = new ProtocolTemporaryGitRepository();
        repository.CommitFile("a.txt", "content");
        using var logDirectory = new TemporaryDirectory();
        await using var engine = await ProtocolTestEngine.StartAsync(logDirectory.DirectoryPath);
        await engine.OpenRepositoryAsync(repository.Path);

        using var response = await engine.SendAsync(
            "analysis.start",
            "analysis-start-stale",
            JsonSerializer.Serialize(
                new
                {
                    path = repository.Path,
                    target = repository.DefaultTarget,
                    freshnessToken = new string('0', 64),
                }));
        var result = ProtocolResponseAssertions.AssertResultEnvelope(response, "analysis-start-stale");
        ProtocolResponseAssertions.AssertExactProperties(result, "state");

        Assert.Equal("rejectedStale", result.GetProperty("state").GetString());
    }

    /// <summary>Asynchronously verifies cancellation before pipeline capture commits no analysis work.</summary>
    /// <returns>A task that represents the asynchronous operation.</returns>
    [Fact]
    public async Task CancelBeforeCaptureCommitsCancelledWithoutRunningAnyStep()
    {
        using var repository = new ProtocolTemporaryGitRepository();
        repository.CommitFile("a.txt", "content");
        using var logDirectory = new TemporaryDirectory();
        await using var engine = await ProtocolTestEngine.StartAsync(logDirectory.DirectoryPath, blockPipelineUntilReleased: true);
        await engine.OpenRepositoryAsync(repository.Path);
        var freshnessToken = await engine.PrepareFreshnessTokenAsync(repository.Path, repository.DefaultTarget);
        using var startResponse = await engine.SendAsync(
            "analysis.start",
            "analysis-cancel-start",
            JsonSerializer.Serialize(
                new
                {
                    path = repository.Path,
                    target = repository.DefaultTarget,
                    freshnessToken,
                }));
        var startResult = ProtocolResponseAssertions.AssertResultEnvelope(startResponse, "analysis-cancel-start");
        ProtocolResponseAssertions.AssertExactProperties(startResult, "state", "runId", "requestedAt");
        var runId = startResult.GetProperty("runId").GetString()!;
        await engine.WaitUntilPipelineBlockedAsync(TimeSpan.FromSeconds(10));

        using var cancelResponse = await engine.SendAsync(
            "analysis.cancel",
            "analysis-cancel-1",
            JsonSerializer.Serialize(new { runId }));
        var cancelResult = ProtocolResponseAssertions.AssertResultEnvelope(cancelResponse, "analysis-cancel-1");
        Assert.Equal(JsonValueKind.Null, cancelResult.ValueKind);
        engine.ReleaseBlockedPipeline();
        using var terminal = await engine.PollUntilTerminalAsync(runId, TimeSpan.FromSeconds(10));

        var result = terminal.RootElement.GetProperty("result");
        Assert.Equal("cancelled", result.GetProperty("state").GetString());
        ProtocolResponseAssertions.AssertExactProperties(result.GetProperty("terminal"), "kind", "terminalAt");
        Assert.False(engine.PipelineStepsStarted);
    }

    /// <summary>Asynchronously verifies a well-formed unknown run identifier maps to the stable unknown-run error.</summary>
    /// <returns>A task that represents the asynchronous operation.</returns>
    [Fact]
    public async Task UnknownRunIdReturnsUnknownRunError()
    {
        using var logDirectory = new TemporaryDirectory();
        await using var engine = await ProtocolTestEngine.StartAsync(logDirectory.DirectoryPath);

        using var response = await engine.SendAsync(
            "analysis.pollRun",
            "analysis-poll-unknown",
            """{"runId":"0198a1b2-3c4d-4e5f-8a9b-0123456789ab"}""");

        var errors = ProtocolResponseAssertions.AssertErrorEnvelope(response, "analysis-poll-unknown");
        var error = Assert.Single(errors.EnumerateArray());
        ProtocolResponseAssertions.AssertExactProperties(error, "type", "code", "message");
        Assert.Equal("analysis.unknownRun", error.GetProperty("code").GetString());
    }

    /// <summary>Asynchronously verifies active-run lookup clears once a run becomes terminal.</summary>
    /// <returns>A task that represents the asynchronous operation.</returns>
    [Fact]
    public async Task GetActiveReportsNoneOnceTheRunReachesTerminal()
    {
        using var repository = new ProtocolTemporaryGitRepository();
        repository.CommitFile("a.txt", "content");
        using var logDirectory = new TemporaryDirectory();
        await using var engine = await ProtocolTestEngine.StartAsync(logDirectory.DirectoryPath, blockPipelineUntilReleased: true);
        await engine.OpenRepositoryAsync(repository.Path);
        var freshnessToken = await engine.PrepareFreshnessTokenAsync(repository.Path, repository.DefaultTarget);
        using var startResponse = await engine.SendAsync(
            "analysis.start",
            "analysis-getactive-start",
            JsonSerializer.Serialize(
                new
                {
                    path = repository.Path,
                    target = repository.DefaultTarget,
                    freshnessToken,
                }));
        var startResult = ProtocolResponseAssertions.AssertResultEnvelope(startResponse, "analysis-getactive-start");
        ProtocolResponseAssertions.AssertExactProperties(startResult, "state", "runId", "requestedAt");
        var runId = startResult.GetProperty("runId").GetString()!;
        await engine.WaitUntilPipelineBlockedAsync(TimeSpan.FromSeconds(10));

        using var activeResponse = await engine.SendAsync(
            "analysis.getActive",
            "analysis-getactive-1",
            JsonSerializer.Serialize(new { path = repository.Path }));
        var activeResult = ProtocolResponseAssertions.AssertResultEnvelope(activeResponse, "analysis-getactive-1");
        ProtocolResponseAssertions.AssertExactProperties(activeResult, "state", "run");
        ProtocolResponseAssertions.AssertAnalysisRunProjection(activeResult.GetProperty("run"));
        Assert.Equal("active", activeResult.GetProperty("state").GetString());
        Assert.Equal(runId, activeResult.GetProperty("run").GetProperty("runId").GetString());

        engine.ReleaseBlockedPipeline();
        using var terminal = await engine.PollUntilTerminalAsync(runId, TimeSpan.FromSeconds(10));
        using var afterTerminalResponse = await engine.SendAsync(
            "analysis.getActive",
            "analysis-getactive-2",
            JsonSerializer.Serialize(new { path = repository.Path }));

        var noneResult = ProtocolResponseAssertions.AssertResultEnvelope(afterTerminalResponse, "analysis-getactive-2");
        ProtocolResponseAssertions.AssertExactProperties(noneResult, "state");
        Assert.Equal("none", noneResult.GetProperty("state").GetString());
    }
}
