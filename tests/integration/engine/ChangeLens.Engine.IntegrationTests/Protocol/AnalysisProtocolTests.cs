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

}
