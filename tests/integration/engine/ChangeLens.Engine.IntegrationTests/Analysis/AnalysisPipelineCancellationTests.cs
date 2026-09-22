using System.Text.Json;
using ChangeLens.Core.AnalysisRuns.Constants;
using ChangeLens.Core.EvidenceBinder.Interfaces;
using ChangeLens.Core.EvidenceBinder.Services;
using ChangeLens.Core.ModelCompletion.Interfaces;
using ChangeLens.Core.ModelCompletion.Models;
using ChangeLens.Core.Results.Models;
using ChangeLens.Core.Snapshots.Interfaces;
using ChangeLens.Core.Snapshots.Services;
using ChangeLens.Engine.IntegrationTests.Analysis.Support;
using ChangeLens.Engine.IntegrationTests.Protocol.Support;
using ChangeLens.Infrastructure.LocalState.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Xunit;
using ModelCompletionModel = ChangeLens.Core.ModelCompletion.Models.ModelCompletion;

namespace ChangeLens.Engine.IntegrationTests.Analysis;

/// <summary>
///     Verifies the production analysis pipeline commits a single cancelled terminal when a run is cancelled while a
///     service is mid-call, and fails cleanly when a captured Git object disappears.
/// </summary>
public sealed class AnalysisPipelineCancellationTests
{
    /// <summary>
    ///     Asynchronously verifies cancelling while the evidence binder is assembling commits one cancelled terminal
    ///     with no stored model and releases the repository for a subsequent run.
    /// </summary>
    /// <returns>A task that represents the asynchronous operation.</returns>
    [Fact]
    public async Task CancelDuringBinderBuildCommitsCancelledAndReleasesTheRepository()
    {
        using var repository = CreateCommittedChangeRepository();
        var resolved = new TaskCompletionSource<GatedEvidenceBinderService>(TaskCreationOptions.RunContinuationsAsynchronously);
        await using var host = await AnalysisPipelineTestHost.CreateAsync(services => services.Replace(
            ServiceDescriptor.Scoped<IEvidenceBinderService>(provider =>
            {
                var instance = new GatedEvidenceBinderService(ActivatorUtilities.CreateInstance<EvidenceBinderService>(provider));
                resolved.TrySetResult(instance);
                return instance;
            })));
        await host.StartAsync(TestContext.Current.CancellationToken);
        await host.OpenRepositoryAsync(repository.Path);
        var freshnessToken = await host.PrepareFreshnessTokenAsync(repository.Path, repository.DefaultTarget);
        var runId = await host.StartAsync(repository.Path, repository.DefaultTarget, freshnessToken);

        var binder = await resolved.Task.WaitAsync(TimeSpan.FromSeconds(60), TestContext.Current.CancellationToken);
        await binder.Entered.WaitAsync(TimeSpan.FromSeconds(60), TestContext.Current.CancellationToken);
        using var cancellation = await host.CancelAsync(runId);

        using var terminal = await host.PollUntilTerminalAsync(runId, TimeSpan.FromSeconds(60));

        var result = terminal.RootElement.GetProperty("result");
        Assert.Equal("cancelled", result.GetProperty("state").GetString());
        Assert.Equal(JsonValueKind.Null, result.GetProperty("readingModel").ValueKind);
        Assert.Equal(JsonValueKind.Null, result.GetProperty("validationRemovals").ValueKind);
        await AssertRepositoryAcceptsAnotherRunAsync(host, repository, runId);
    }

    /// <summary>
    ///     Asynchronously verifies cancelling while the curator waits on the model provider commits one cancelled
    ///     terminal with the discovery facts retained and no stored model.
    /// </summary>
    /// <returns>A task that represents the asynchronous operation.</returns>
    [Fact]
    public async Task CancelDuringCuratorWaitCommitsCancelledWithDiscoveryFacts()
    {
        using var repository = CreateCommittedChangeRepository();
        var client = new CancellationAwaitingModelCompletionClient();
        await using var host = await AnalysisPipelineTestHost.CreateAsync(
            services => services.Replace(ServiceDescriptor.Scoped<IModelCompletionClient>(_ => client)));
        await host.StartAsync(TestContext.Current.CancellationToken);
        await host.OpenRepositoryAsync(repository.Path);
        var freshnessToken = await host.PrepareFreshnessTokenAsync(repository.Path, repository.DefaultTarget);
        var runId = await host.StartAsync(repository.Path, repository.DefaultTarget, freshnessToken);

        await client.Entered.WaitAsync(TimeSpan.FromSeconds(60), TestContext.Current.CancellationToken);
        using var cancellation = await host.CancelAsync(runId);

        using var terminal = await host.PollUntilTerminalAsync(runId, TimeSpan.FromSeconds(60));

        var result = terminal.RootElement.GetProperty("result");
        Assert.Equal("cancelled", result.GetProperty("state").GetString());
        Assert.Equal(JsonValueKind.Null, result.GetProperty("readingModel").ValueKind);
        Assert.Equal(JsonValueKind.Null, result.GetProperty("validationRemovals").ValueKind);
        AssertTerminalFactPresent(result, "correspondenceCandidates");
        AssertTerminalFactPresent(result, "disclosedEvidenceNodes");
        await AssertRepositoryAcceptsAnotherRunAsync(host, repository, runId);
    }

    /// <summary>
    ///     Asynchronously verifies a captured Git object deleted after capture fails the run as a stale snapshot and
    ///     never quotes the still-present worktree file.
    /// </summary>
    /// <returns>A task that represents the asynchronous operation.</returns>
    [Fact]
    public async Task MissingCapturedGitObjectFailsAsStaleWithoutReadingTheWorktree()
    {
        var marker = $"worktree-quote-marker-{Guid.NewGuid():N}";
        using var repository = CreateMissingObjectRepository(marker);
        var resolved = new TaskCompletionSource<CaptureThenHoldSnapshotCaptureService>(TaskCreationOptions.RunContinuationsAsynchronously);
        await using var host = await AnalysisPipelineTestHost.CreateAsync(services => services.Replace(
            ServiceDescriptor.Scoped<ISnapshotCaptureService>(provider =>
            {
                var instance = new CaptureThenHoldSnapshotCaptureService(ActivatorUtilities.CreateInstance<GitSnapshotCaptureService>(provider));
                resolved.TrySetResult(instance);
                return instance;
            })));
        await host.StartAsync(TestContext.Current.CancellationToken);
        await host.OpenRepositoryAsync(repository.Path);
        var freshnessToken = await host.PrepareFreshnessTokenAsync(repository.Path, repository.DefaultTarget);
        var runId = await host.StartAsync(repository.Path, repository.DefaultTarget, freshnessToken);

        var heldCapture = await resolved.Task.WaitAsync(TimeSpan.FromSeconds(60), TestContext.Current.CancellationToken);
        await heldCapture.Captured.WaitAsync(TimeSpan.FromSeconds(60), TestContext.Current.CancellationToken);
        var entry = heldCapture.Capture!.Manifest.Entries.First(
            manifestEntry => manifestEntry.Path.EndsWith(".txt", StringComparison.Ordinal)
                && manifestEntry.HeadObjectId.Any(character => character != '0'));
        var objectPath = Path.Combine(
            repository.Path, ".git", "objects", entry.HeadObjectId[..2], entry.HeadObjectId[2..]);
        Assert.True(
            File.Exists(objectPath),
            $"The captured object {entry.HeadObjectId} for {entry.Path} is not stored loose at {objectPath}; "
            + "the missing-object scenario requires a loose object.");
        File.Delete(objectPath);
        heldCapture.Release();

        using var terminal = await host.PollUntilTerminalAsync(runId, TimeSpan.FromSeconds(60));

        var result = terminal.RootElement.GetProperty("result");
        Assert.Equal("failed", result.GetProperty("state").GetString());
        Assert.Equal("snapshot.staleObject", result.GetProperty("terminal").GetProperty("failureCode").GetString());
        Assert.Equal(JsonValueKind.Null, result.GetProperty("readingModel").ValueKind);
        Assert.DoesNotContain(marker, terminal.RootElement.GetRawText(), StringComparison.Ordinal);
    }

    /// <summary>
    ///     Asynchronously verifies a completed scripted run persisted non-null start and finish timestamps for the
    ///     discover and collect lifecycle steps.
    /// </summary>
    /// <returns>A task that represents the asynchronous operation.</returns>
    [Fact]
    public async Task DiscoverAndCollectStepsRecordStartAndFinishTimestamps()
    {
        using var repository = CreateCommittedChangeRepository();
        var client = new ScriptedModelCompletionClient(DraftForBinder);
        await using var host = await AnalysisPipelineTestHost.CreateAsync(
            services => services.Replace(ServiceDescriptor.Scoped<IModelCompletionClient>(_ => client)));
        await host.StartAsync(TestContext.Current.CancellationToken);
        await host.OpenRepositoryAsync(repository.Path);
        var freshnessToken = await host.PrepareFreshnessTokenAsync(repository.Path, repository.DefaultTarget);
        var runId = await host.StartAsync(repository.Path, repository.DefaultTarget, freshnessToken);

        using var terminal = await host.PollUntilTerminalAsync(runId, TimeSpan.FromSeconds(60));

        var result = terminal.RootElement.GetProperty("result");
        var state = result.GetProperty("state").GetString();
        Assert.True(
            state is "completed" or "completedWithLimitations",
            $"The scripted run reached unexpected state '{state}'.");

        var steps = await ReadAnalysisRunStepRowsAsync(host, runId);
        var discover = Assert.Single(steps, step => step.StepId == AnalysisStepId.Discover);
        var collect = Assert.Single(steps, step => step.StepId == AnalysisStepId.Collect);
        Assert.NotNull(discover.StartedAtUnixMilliseconds);
        Assert.NotNull(discover.FinishedAtUnixMilliseconds);
        Assert.NotNull(collect.StartedAtUnixMilliseconds);
        Assert.NotNull(collect.FinishedAtUnixMilliseconds);
    }

    private static async Task AssertRepositoryAcceptsAnotherRunAsync(
        AnalysisPipelineTestHost host,
        ProtocolTemporaryGitRepository repository,
        string firstRunId)
    {
        var freshnessToken = await host.PrepareFreshnessTokenAsync(repository.Path, repository.DefaultTarget);
        var secondRunId = await host.StartAsync(repository.Path, repository.DefaultTarget, freshnessToken);

        Assert.NotEqual(firstRunId, secondRunId);
        using var cancellation = await host.CancelAsync(secondRunId);
        using var terminal = await host.PollUntilTerminalAsync(secondRunId, TimeSpan.FromSeconds(60));
        Assert.Equal("cancelled", terminal.RootElement.GetProperty("result").GetProperty("state").GetString());
    }

    private static async Task<IReadOnlyList<AnalysisRunStepRow>> ReadAnalysisRunStepRowsAsync(AnalysisPipelineTestHost host, string runId)
    {
        await using var scope = host.Host.Services.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<ChangeLensLocalStateDbContext>();
        return await context.Database.SqlQueryRaw<AnalysisRunStepRow>(
                "SELECT step_id AS StepId, started_at_unix_ms AS StartedAtUnixMilliseconds, "
                + "finished_at_unix_ms AS FinishedAtUnixMilliseconds FROM analysis_run_steps WHERE run_id = {0}",
                runId)
            .ToListAsync(TestContext.Current.CancellationToken);
    }

    private static void AssertTerminalFactPresent(JsonElement result, string kind) =>
        Assert.Contains(result.GetProperty("facts").EnumerateArray(), fact => fact.GetProperty("kind").GetString() == kind);

    private static Result<ModelCompletionModel> DraftForBinder(ModelCompletionRequest request)
    {
        using var binder = JsonDocument.Parse(request.UserMessage);
        var evidence = binder.RootElement.GetProperty("evidence").EnumerateArray().ToArray();
        var marker = evidence.FirstOrDefault(node => Text(node).Contains("committed-marker", StringComparison.Ordinal));
        var nodeId = (marker.ValueKind == JsonValueKind.Undefined ? evidence[0] : marker).GetProperty("nodeId").GetString()!;
        return Result.Success(Completion(DraftJson(nodeId, evidence[0].GetProperty("nodeId").GetString()!)));
    }

    private static string DraftJson(string thesisNodeId, string trackNodeId) =>
        JsonSerializer.Serialize(new
        {
            thesis = new { text = "The committed change matters.", evidenceNodeIds = new[] { thesisNodeId } },
            tracks = new object[]
            {
                new
                {
                    id = "t1",
                    title = "Committed change",
                    summary = new { text = "The change is described.", evidenceNodeIds = new[] { trackNodeId } },
                    shape = "Walk",
                    participants = new object[]
                    {
                        new
                        {
                            id = "p1",
                            name = "Participant",
                            role = "Actor",
                            changed = true,
                            evidenceNodeIds = new[] { trackNodeId },
                        },
                    },
                    relationships = Array.Empty<object>(),
                    orderedSteps = new object[]
                    {
                        new { text = "The first step.", evidenceNodeIds = new[] { trackNodeId } },
                    },
                    purposes = Array.Empty<object>(),
                },
            },
            droppedNodeIds = Array.Empty<string>(),
        });

    private static ModelCompletionModel Completion(string text) => new("scripted-model", text, 1.0, 1, 1, null, null);

    private static string Text(JsonElement evidence) => evidence.GetProperty("text").GetString() ?? string.Empty;

    private static ProtocolTemporaryGitRepository CreateCommittedChangeRepository()
    {
        var repository = new ProtocolTemporaryGitRepository();
        repository.CommitFile("src/app.txt", "public static string Value()\n{\n    return \"original\";\n}\n");
        repository.CommitFileAtHead("src/app.txt", "public static string Value()\n{\n    return \"committed-marker\";\n}\n");
        return repository;
    }

    private static ProtocolTemporaryGitRepository CreateMissingObjectRepository(string marker)
    {
        var repository = new ProtocolTemporaryGitRepository();
        repository.CommitFile("src/app.txt", "public static string Value()\n{\n    return \"original\";\n}\n");
        repository.CommitFileAtHead(
            "src/app.txt", $"public static string Value()\n{{\n    return \"{marker}\";\n}}\n");
        return repository;
    }
}