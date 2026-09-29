using System.Text;
using System.Text.Json;
using ChangeLens.Core.AnalysisRuns.Constants;
using ChangeLens.Core.ContextPolicy.Constants;
using ChangeLens.Core.EvidenceBinder.Constants;
using ChangeLens.Core.Review.Constants;
using ChangeLens.Core.Snapshots.Constants;
using ChangeLens.Engine.IntegrationTests.Analysis.Support;
using Xunit;

namespace ChangeLens.Engine.IntegrationTests.Analysis;

/// <summary>
///     Verifies the collect step drops the review's whole contribution, and only the review's, when the reading projection
///     does not fit the poll response budget.
/// </summary>
/// <remarks>
///     The 2 MB budget is out of reach at the default limits, so these runs raise the context window, the blob limit, and the
///     per-node disclosure limit through configuration and use fixtures large enough to cross the budget.
/// </remarks>
public sealed class AnalysisPipelineReviewBudgetTests
{
    private const int WithheldFindingCount = 45_000;

    private static readonly TimeSpan Deadline = TimeSpan.FromSeconds(120);

    private static readonly IReadOnlyDictionary<string, string?> LargeQuoteSettings = new Dictionary<string, string?>
    {
        [FrozenGitTreeReaderConfigurationConstants.MaximumBlobBytesKey] = "4000000",
        [ContextPolicyConfigurationConstants.MaximumDisclosedCharactersPerNodeKey] = "4000000",
        [EvidenceBinderConfigurationConstants.ContextWindowTokensKey] = "3000000",
    };

    private static readonly IReadOnlyDictionary<string, string?> LargeReplySettings = new Dictionary<string, string?>
    {
        [EvidenceBinderConfigurationConstants.ContextWindowTokensKey] = "3000000",
        [EvidenceBinderConfigurationConstants.CuratorOutputCharactersKey] = "8000000",
        [ReviewerConfigurationConstants.MaximumOutputCharactersKey] = "8000000",
    };

    /// <summary>
    ///     Asynchronously verifies findings whose cited evidence overflows the budget are dropped with that evidence and the
    ///     run ends completed with limitations.
    /// </summary>
    /// <returns>A task that represents the asynchronous operation.</returns>
    [Fact]
    public async Task PublishedFindingsThatOverflowFallBackToTheExplanationAlone()
    {
        var client = new ScriptedModelCompletionClient(
            ScriptedReplies.Curator,
            request => ScriptedReplies.Reviewer(ScriptedReplies.FindingCiting(request, "f1", "src/big.txt")));
        using var repository = PipelineRepositories.CreateCommittedChangeWithOversizedQuote();

        using var terminal = await ScriptedPipelineRun.RunToTerminalAsync(repository, client, LargeQuoteSettings, Deadline);

        var result = terminal.RootElement.GetProperty("result");
        Assert.Equal("completedWithLimitations", result.GetProperty("state").GetString());
        Assert.Equal(1, result.GetProperty("terminal").GetProperty("limitationCount").GetInt32());
        var readingModel = result.GetProperty("readingModel");
        AssertFallbackCarriesNoReview(readingModel);
        Assert.DoesNotContain(
            readingModel.GetProperty("evidence").EnumerateArray(), node => node.GetProperty("path").GetString() == "src/big.txt");
        Assert.Contains(
            readingModel.GetProperty("evidence").EnumerateArray(), node => node.GetProperty("path").GetString() == "src/app.txt");
        Assert.Empty(result.GetProperty("validationRemovals").EnumerateArray());
        Assert.Equal(1, client.ReviewerCallCount);
    }

    /// <summary>
    ///     Asynchronously verifies reviewer removal records that overflow the budget are dropped, while the run summary still
    ///     counts them.
    /// </summary>
    /// <returns>A task that represents the asynchronous operation.</returns>
    [Fact]
    public async Task ReviewerRemovalRecordsThatOverflowFallBackToTheExplanationAlone()
    {
        var client = new ScriptedModelCompletionClient(ScriptedReplies.Curator, _ => ScriptedReplies.Success(WithheldFindingsJson()));
        using var repository = PipelineRepositories.CreateCommittedChange();

        using var terminal = await ScriptedPipelineRun.RunToTerminalAsync(repository, client, LargeReplySettings, Deadline);

        var result = terminal.RootElement.GetProperty("result");
        Assert.Equal("completedWithLimitations", result.GetProperty("state").GetString());
        AssertFallbackCarriesNoReview(result.GetProperty("readingModel"));
        Assert.Empty(result.GetProperty("validationRemovals").EnumerateArray());
        var removalFact = Assert.Single(
            result.GetProperty("facts").EnumerateArray(), fact => fact.GetProperty("kind").GetString() == "validationRemovals");
        Assert.Equal(WithheldFindingCount, removalFact.GetProperty("count").GetInt32());
    }

    /// <summary>
    ///     Asynchronously verifies a run whose explanation alone exceeds the budget still fails with the reading-model-too-large
    ///     code, because dropping the review cannot make it fit.
    /// </summary>
    /// <returns>A task that represents the asynchronous operation.</returns>
    [Fact]
    public async Task FallbackThatStillDoesNotFitFailsTheRun()
    {
        var client = new ScriptedModelCompletionClient(
            request => ScriptedReplies.Success(ScriptedReplies.CuratorJson(request, ScriptedReplies.FindNodeId(request, "src/big.txt"))),
            request => ScriptedReplies.Reviewer(ScriptedReplies.ValidFinding(request, "f1")));
        using var repository = PipelineRepositories.CreateCommittedChangeWithOversizedQuote();

        using var terminal = await ScriptedPipelineRun.RunToTerminalAsync(repository, client, LargeQuoteSettings, Deadline);

        var result = terminal.RootElement.GetProperty("result");
        Assert.Equal("failed", result.GetProperty("state").GetString());
        Assert.Equal(AnalysisFailureCode.ReadingModelTooLarge, result.GetProperty("terminal").GetProperty("failureCode").GetString());
        Assert.Equal(JsonValueKind.Null, result.GetProperty("readingModel").ValueKind);
    }

    private static void AssertFallbackCarriesNoReview(JsonElement readingModel)
    {
        var review = readingModel.GetProperty("review");
        Assert.Equal("tooLarge", review.GetProperty("status").GetString());
        Assert.Equal(JsonValueKind.Null, review.GetProperty("recommendation").ValueKind);
        Assert.Equal(0, review.GetProperty("withheldCount").GetInt32());
        Assert.Empty(readingModel.GetProperty("findings").EnumerateArray());
        Assert.Equal(JsonValueKind.Object, readingModel.GetProperty("thesis").ValueKind);
        Assert.Contains(
            readingModel.GetProperty("assurances").EnumerateArray(), assurance => assurance.GetProperty("kind").GetString() == "reviewTooLarge");
    }

    private static string WithheldFindingsJson()
    {
        var reply = new StringBuilder("{\"findings\":[");
        for (var index = 0; index < WithheldFindingCount; index++)
        {
            reply.Append(index == 0 ? string.Empty : ",");
            reply.Append(
                $"{{\"id\":\"f{index}\",\"severity\":\"info\",\"title\":\"t\",\"trigger\":\"t\",\"impact\":\"i\",\"fix\":\"f\","
                + "\"evidenceNodeIds\":[\"undisclosed-node\"],\"anchor\":{\"nodeId\":\"undisclosed-node\",\"lines\":\"l\"}}");
        }

        return reply.Append("]}").ToString();
    }
}
