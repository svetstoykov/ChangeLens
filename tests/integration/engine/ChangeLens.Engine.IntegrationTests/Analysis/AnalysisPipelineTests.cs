using System.Text.Json;
using ChangeLens.Core.AnalysisRuns.Constants;
using ChangeLens.Core.AnalysisRuns.Models;
using ChangeLens.Core.ModelCompletion.Constants;
using ChangeLens.Core.ModelCompletion.Interfaces;
using ChangeLens.Core.ModelCompletion.Models;
using ChangeLens.Core.Results.Models;
using ChangeLens.Engine.AnalysisRuns.Helpers;
using ChangeLens.Engine.AnalysisRuns.Models;
using ChangeLens.Engine.IntegrationTests.Analysis.Support;
using ChangeLens.Engine.IntegrationTests.Protocol.Support;
using ChangeLens.Engine.Protocol.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Xunit;
using ModelCompletionModel = ChangeLens.Core.ModelCompletion.Models.ModelCompletion;

namespace ChangeLens.Engine.IntegrationTests.Analysis;

/// <summary>
///     Verifies the production analysis pipeline end to end over real Git capture and inline protocol polling.
/// </summary>
public sealed class AnalysisPipelineTests
{
    /// <summary>Asynchronously verifies a mixed committed change publishes a reading model and its removals.</summary>
    /// <returns>A task that represents the asynchronous operation.</returns>
    [Fact]
    public async Task MixedCommittedChangeCompletesWithAReadingModel()
    {
        using var repository = CreateMixedChangeRepository();
        var client = new ScriptedModelCompletionClient(DraftForBinder);
        await using var host = await AnalysisPipelineTestHost.CreateAsync(
            services => services.Replace(ServiceDescriptor.Scoped<IModelCompletionClient>(_ => client)));
        await host.StartAsync(TestContext.Current.CancellationToken);
        await host.OpenRepositoryAsync(repository.Path);
        var freshnessToken = await host.PrepareFreshnessTokenAsync(repository.Path, repository.DefaultTarget);
        var runId = await host.StartAsync(repository.Path, repository.DefaultTarget, freshnessToken);

        using var terminal = await host.PollUntilTerminalAsync(runId, TimeSpan.FromSeconds(60));

        var result = terminal.RootElement.GetProperty("result");
        Assert.Equal("completedWithLimitations", result.GetProperty("state").GetString());
        AssertTerminalFactPresent(result, "excludedUncommittedFiles");
        AssertTerminalFactPresent(result, "correspondenceCandidates");
        AssertTerminalFactPresent(result, "disclosedEvidenceNodes");
        AssertTerminalFactPresent(result, "validationRemovals");
        var readingModel = result.GetProperty("readingModel");
        var evidence = readingModel.GetProperty("evidence").EnumerateArray().ToArray();
        Assert.Contains(evidence, node => Text(node).Contains("committed-marker", StringComparison.Ordinal));
        Assert.DoesNotContain(evidence, node => Text(node).Contains("unstaged-marker", StringComparison.Ordinal));
        var limitations = readingModel.GetProperty("limitations").EnumerateArray().ToArray();
        Assert.Contains(limitations, limitation =>
            limitation.GetProperty("kind").GetString() == "uncommittedWorkExcluded"
            && limitation.GetProperty("path").ValueKind == JsonValueKind.Null);
        Assert.Contains(limitations, limitation =>
            limitation.GetProperty("path").GetString() == "assets/blob.bin"
            && limitation.GetProperty("kind").GetString() == "fileNotRead");
        var omissionSummaries = readingModel.GetProperty("omissionSummaries").EnumerateArray().ToArray();
        Assert.NotEmpty(omissionSummaries);
        Assert.All(omissionSummaries, summary =>
        {
            Assert.True(summary.TryGetProperty("totalCount", out _));
            Assert.True(summary.TryGetProperty("sampleCount", out _));
            Assert.True(summary.TryGetProperty("resolvedSampleCount", out _));
        });
        Assert.All(Statements(readingModel), statement => Assert.Equal("unchecked", statement.GetProperty("trust").GetString()));
        Assert.All(
            readingModel.GetProperty("areas").EnumerateArray()
                .SelectMany(area => area.GetProperty("relationships").EnumerateArray()),
            relationship => Assert.Equal("unchecked", relationship.GetProperty("trust").GetString()));
        var citations = readingModel.GetProperty("citations").EnumerateArray().ToArray();
        Assert.All(citations, citation =>
        {
            Assert.Equal("unchecked", citation.GetProperty("provenance").GetString());
            Assert.Empty(citation.GetProperty("focus").EnumerateArray());
        });
        var removals = result.GetProperty("validationRemovals").EnumerateArray().ToArray();
        var thesisRemoval = Assert.Single(removals, removal =>
            removal.GetProperty("scope").GetString() == "statement" && removal.GetProperty("id").GetString() == "thesis");
        Assert.Contains("missing", thesisRemoval.GetProperty("reason").GetString()!, StringComparison.Ordinal);
        Assert.Equal(1, client.CallCount);
        await host.StopAsync(TestContext.Current.CancellationToken);
    }

    /// <summary>Asynchronously verifies an unreadable curator draft fails the run without a reading model.</summary>
    /// <returns>A task that represents the asynchronous operation.</returns>
    [Fact]
    public async Task UnreadableCuratorOutputFailsTheRun()
    {
        using var repository = CreateCommittedChangeRepository();
        var client = new ScriptedModelCompletionClient(_ => Result.Success(Completion("not a draft")));
        await using var host = await AnalysisPipelineTestHost.CreateAsync(
            services => services.Replace(ServiceDescriptor.Scoped<IModelCompletionClient>(_ => client)));
        await host.StartAsync(TestContext.Current.CancellationToken);
        await host.OpenRepositoryAsync(repository.Path);
        var freshnessToken = await host.PrepareFreshnessTokenAsync(repository.Path, repository.DefaultTarget);
        var runId = await host.StartAsync(repository.Path, repository.DefaultTarget, freshnessToken);

        using var terminal = await host.PollUntilTerminalAsync(runId, TimeSpan.FromSeconds(30));

        var result = terminal.RootElement.GetProperty("result");
        Assert.Equal("failed", result.GetProperty("state").GetString());
        Assert.Equal("analysis.curatorOutputUnreadable", result.GetProperty("terminal").GetProperty("failureCode").GetString());
        Assert.Equal(JsonValueKind.Null, result.GetProperty("readingModel").ValueKind);
        Assert.Equal(JsonValueKind.Null, result.GetProperty("validationRemovals").ValueKind);
        AssertTerminalFactPresent(result, "correspondenceCandidates");
        AssertTerminalFactPresent(result, "disclosedEvidenceNodes");
        await host.StopAsync(TestContext.Current.CancellationToken);
    }

    /// <summary>Asynchronously verifies a provider failure forwards its stable code and stores no model.</summary>
    /// <returns>A task that represents the asynchronous operation.</returns>
    [Fact]
    public async Task ProviderFailureForwardsItsCodeWithoutAModel()
    {
        using var repository = CreateCommittedChangeRepository();
        var client = new ScriptedModelCompletionClient(_ => Result.Fail<ModelCompletionModel>(
            OperationError.ExternalDependencyFailure("provider unavailable", ModelCompletionErrorCode.ProviderUnavailable)));
        await using var host = await AnalysisPipelineTestHost.CreateAsync(
            services => services.Replace(ServiceDescriptor.Scoped<IModelCompletionClient>(_ => client)));
        await host.StartAsync(TestContext.Current.CancellationToken);
        await host.OpenRepositoryAsync(repository.Path);
        var freshnessToken = await host.PrepareFreshnessTokenAsync(repository.Path, repository.DefaultTarget);
        var runId = await host.StartAsync(repository.Path, repository.DefaultTarget, freshnessToken);

        using var terminal = await host.PollUntilTerminalAsync(runId, TimeSpan.FromSeconds(30));

        var result = terminal.RootElement.GetProperty("result");
        Assert.Equal("failed", result.GetProperty("state").GetString());
        Assert.Equal("modelCompletion.providerUnavailable", result.GetProperty("terminal").GetProperty("failureCode").GetString());
        Assert.Equal(JsonValueKind.Null, result.GetProperty("readingModel").ValueKind);
        AssertTerminalFactPresent(result, "correspondenceCandidates");
        AssertTerminalFactPresent(result, "disclosedEvidenceNodes");
        await host.StopAsync(TestContext.Current.CancellationToken);
    }

    /// <summary>Asynchronously verifies the default provider configuration fails the run as not configured.</summary>
    /// <returns>A task that represents the asynchronous operation.</returns>
    [Fact]
    public async Task DefaultProviderConfigurationFailsAsNotConfigured()
    {
        using var repository = CreateCommittedChangeRepository();
        await using var host = await AnalysisPipelineTestHost.CreateAsync();
        await host.StartAsync(TestContext.Current.CancellationToken);
        await host.OpenRepositoryAsync(repository.Path);
        var freshnessToken = await host.PrepareFreshnessTokenAsync(repository.Path, repository.DefaultTarget);
        var runId = await host.StartAsync(repository.Path, repository.DefaultTarget, freshnessToken);

        using var terminal = await host.PollUntilTerminalAsync(runId, TimeSpan.FromSeconds(30));

        var result = terminal.RootElement.GetProperty("result");
        Assert.Equal("failed", result.GetProperty("state").GetString());
        Assert.Equal("modelCompletion.notConfigured", result.GetProperty("terminal").GetProperty("failureCode").GetString());
        Assert.Equal(JsonValueKind.Null, result.GetProperty("readingModel").ValueKind);
        await host.StopAsync(TestContext.Current.CancellationToken);
    }

    /// <summary>Verifies the projection budget rejects an over-limit model and accepts a large but bounded one.</summary>
    [Fact]
    public void ReadingProjectionBudgetEnforcesThePollResponseLimit()
    {
        var oversized = new AnalysisReadingProjection(new string('a', AnalysisRunLimits.MaximumPollResponseBytes), string.Empty);
        Assert.False(ReadingProjectionBudget.Fits(oversized));

        var evidence = new ReadingEvidenceResult(
            "n1", "a.txt", "after", 1, 1, true, false, false, new string('x', 8_000));
        var model = new ReadingModelResult(null, [], [], [evidence], [], [], []);
        var serialized = new EngineProtocolSerializer().SerializeDocument(model);
        Assert.True(serialized.IsSuccess);
        Assert.True(serialized.Data!.Length > 8_000);
        Assert.True(ReadingProjectionBudget.Fits(new AnalysisReadingProjection(serialized.Data!, "[]")));
    }

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
            thesis = new { text = "The committed change matters.", evidenceNodeIds = new[] { thesisNodeId, "missing" } },
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

    private static IEnumerable<JsonElement> Statements(JsonElement readingModel)
    {
        if (readingModel.GetProperty("thesis").ValueKind == JsonValueKind.Object)
        {
            yield return readingModel.GetProperty("thesis");
        }

        foreach (var area in readingModel.GetProperty("areas").EnumerateArray())
        {
            if (area.GetProperty("summary").ValueKind == JsonValueKind.Object)
            {
                yield return area.GetProperty("summary");
            }

            foreach (var step in area.GetProperty("orderedSteps").EnumerateArray())
            {
                yield return step;
            }

            foreach (var purpose in area.GetProperty("purposes").EnumerateArray())
            {
                yield return purpose;
            }
        }
    }

    private static void AssertTerminalFactPresent(JsonElement result, string kind) =>
        Assert.Contains(result.GetProperty("facts").EnumerateArray(), fact => fact.GetProperty("kind").GetString() == kind);

    private static ProtocolTemporaryGitRepository CreateCommittedChangeRepository()
    {
        var repository = new ProtocolTemporaryGitRepository();
        repository.CommitFile("src/app.txt", "public static string Value()\n{\n    return \"original\";\n}\n");
        repository.CommitFileAtHead("src/app.txt", "public static string Value()\n{\n    return \"committed-marker\";\n}\n");
        return repository;
    }

    private static ProtocolTemporaryGitRepository CreateMixedChangeRepository()
    {
        var repository = CreateCommittedChangeRepository();
        repository.CommitRenameAtHead("src/app.txt", "src/app-renamed.txt");
        repository.CommitBinaryFileAtHead("assets/blob.bin", [0x00, 0x01, 0x02, 0x03]);
        repository.WriteTextFile("src/app-renamed.txt", "public static string Value()\n{\n    return \"committed-marker\";\n}\n" +
            "// unstaged-marker\n");
        return repository;
    }
}