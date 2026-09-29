using System.Text.Json;
using ChangeLens.Core.ModelCompletion.Constants;
using ChangeLens.Core.ModelCompletion.Interfaces;
using ChangeLens.Core.Results.Models;
using ChangeLens.Engine.IntegrationTests.Analysis.Support;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Xunit;
using ModelCompletionModel = ChangeLens.Core.ModelCompletion.Models.ModelCompletion;

namespace ChangeLens.Engine.IntegrationTests.Analysis;

/// <summary>
///     Verifies the collect step runs the curator and the reviewer together and maps each reviewer result onto the
///     published review and the run's terminal state.
/// </summary>
public sealed class AnalysisPipelineReviewTests
{
    private static readonly TimeSpan Deadline = TimeSpan.FromSeconds(30);

    /// <summary>Asynchronously verifies neither call waits for the other: each reply is held until the other request arrives.</summary>
    /// <returns>A task that represents the asynchronous operation.</returns>
    [Fact]
    public async Task CuratorAndReviewerRunTogether()
    {
        var curatorEntered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var reviewerEntered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var client = new ScriptedModelCompletionClient(
            async (request, cancellationToken) =>
            {
                curatorEntered.TrySetResult();
                await reviewerEntered.Task.WaitAsync(Deadline, cancellationToken);
                return ScriptedReplies.Curator(request);
            },
            async (request, cancellationToken) =>
            {
                reviewerEntered.TrySetResult();
                await curatorEntered.Task.WaitAsync(Deadline, cancellationToken);
                return ScriptedReplies.Reviewer(ScriptedReplies.ValidFinding(request, "f1"));
            });

        using var terminal = await RunToTerminalAsync(client);

        var result = terminal.RootElement.GetProperty("result");
        Assert.Equal("completed", result.GetProperty("state").GetString());
        Assert.Equal("ran", result.GetProperty("readingModel").GetProperty("review").GetProperty("status").GetString());
        Assert.Equal(1, client.CuratorCallCount);
        Assert.Equal(1, client.ReviewerCallCount);
    }

    /// <summary>
    ///     Asynchronously verifies a reviewer provider error, an unreadable reply, and an empty object each publish the
    ///     explanation with a failed review and end the run completed with limitations.
    /// </summary>
    /// <param name="scenario">The reviewer failure to script.</param>
    /// <returns>A task that represents the asynchronous operation.</returns>
    [Theory]
    [InlineData("providerError")]
    [InlineData("unreadableReply")]
    [InlineData("emptyObject")]
    public async Task ReviewerFailurePublishesTheExplanationWithReviewFailed(string scenario)
    {
        var client = new ScriptedModelCompletionClient(
            ScriptedReplies.Curator,
            _ => scenario switch
            {
                "providerError" => Result.Fail<ModelCompletionModel>(
                    OperationError.ExternalDependencyFailure("provider unavailable", ModelCompletionErrorCode.ProviderUnavailable)),
                "unreadableReply" => ScriptedReplies.Success("not json"),
                _ => ScriptedReplies.Success("{}"),
            });

        using var terminal = await RunToTerminalAsync(client);

        var result = terminal.RootElement.GetProperty("result");
        Assert.Equal("completedWithLimitations", result.GetProperty("state").GetString());
        Assert.Equal(1, result.GetProperty("terminal").GetProperty("limitationCount").GetInt32());
        var readingModel = result.GetProperty("readingModel");
        Assert.Equal("failed", readingModel.GetProperty("review").GetProperty("status").GetString());
        Assert.Equal(JsonValueKind.Null, readingModel.GetProperty("review").GetProperty("recommendation").ValueKind);
        Assert.Equal(0, readingModel.GetProperty("review").GetProperty("withheldCount").GetInt32());
        Assert.Empty(readingModel.GetProperty("findings").EnumerateArray());
        Assert.Equal(JsonValueKind.Object, readingModel.GetProperty("thesis").ValueKind);
        Assert.Contains(
            readingModel.GetProperty("assurances").EnumerateArray(), assurance => assurance.GetProperty("kind").GetString() == "reviewFailed");
        Assert.Empty(result.GetProperty("validationRemovals").EnumerateArray());
        Assert.Equal(1, client.ReviewerCallCount);
    }

    /// <summary>Asynchronously verifies a disabled reviewer makes one provider call and publishes a review that was not run.</summary>
    /// <returns>A task that represents the asynchronous operation.</returns>
    [Fact]
    public async Task DisabledReviewMakesOneProviderCallAndPublishesNotRun()
    {
        var client = new ScriptedModelCompletionClient(ScriptedReplies.Curator);

        using var terminal = await RunToTerminalAsync(client, AnalysisPipelineTestHost.ReviewDisabled);

        var result = terminal.RootElement.GetProperty("result");
        Assert.Equal("completed", result.GetProperty("state").GetString());
        var review = result.GetProperty("readingModel").GetProperty("review");
        Assert.Equal("notRun", review.GetProperty("status").GetString());
        Assert.Equal(JsonValueKind.Null, review.GetProperty("recommendation").ValueKind);
        Assert.Equal(1, client.CallCount);
        Assert.Equal(0, client.ReviewerCallCount);
    }

    /// <summary>Asynchronously verifies a valid finding publishes and a withheld one joins the run's removal count and records.</summary>
    /// <returns>A task that represents the asynchronous operation.</returns>
    [Fact]
    public async Task PublishedAndWithheldFindingsReachTheReadingModelAndTheRemovalCount()
    {
        var client = new ScriptedModelCompletionClient(
            ScriptedReplies.Curator,
            request => ScriptedReplies.Reviewer(ScriptedReplies.ValidFinding(request, "f1"), ScriptedReplies.UndisclosedFinding("f2")));

        using var terminal = await RunToTerminalAsync(client);

        var result = terminal.RootElement.GetProperty("result");
        Assert.Equal("completed", result.GetProperty("state").GetString());
        var readingModel = result.GetProperty("readingModel");
        var finding = Assert.Single(readingModel.GetProperty("findings").EnumerateArray());
        Assert.Equal("f1", finding.GetProperty("id").GetString());
        var review = readingModel.GetProperty("review");
        Assert.Equal("ran", review.GetProperty("status").GetString());
        Assert.Equal("issuesWorthAddressing", review.GetProperty("recommendation").GetString());
        Assert.Equal(1, review.GetProperty("withheldCount").GetInt32());
        var removal = Assert.Single(result.GetProperty("validationRemovals").EnumerateArray());
        Assert.Equal("finding", removal.GetProperty("scope").GetString());
        Assert.Equal("f2", removal.GetProperty("id").GetString());
        var removalFact = Assert.Single(
            result.GetProperty("facts").EnumerateArray(), fact => fact.GetProperty("kind").GetString() == "validationRemovals");
        Assert.Equal(1, removalFact.GetProperty("count").GetInt32());
    }

    /// <summary>Asynchronously verifies a review whose every finding is withheld reads as no defects confirmed.</summary>
    /// <returns>A task that represents the asynchronous operation.</returns>
    [Fact]
    public async Task EveryFindingWithheldGivesNoDefectsConfirmed()
    {
        var client = new ScriptedModelCompletionClient(
            ScriptedReplies.Curator,
            _ => ScriptedReplies.Reviewer(ScriptedReplies.UndisclosedFinding("f1")));

        using var terminal = await RunToTerminalAsync(client);

        var result = terminal.RootElement.GetProperty("result");
        Assert.Equal("completed", result.GetProperty("state").GetString());
        var readingModel = result.GetProperty("readingModel");
        Assert.Empty(readingModel.GetProperty("findings").EnumerateArray());
        var review = readingModel.GetProperty("review");
        Assert.Equal("ran", review.GetProperty("status").GetString());
        Assert.Equal("noDefectsConfirmed", review.GetProperty("recommendation").GetString());
        Assert.Equal(1, review.GetProperty("withheldCount").GetInt32());
    }

    /// <summary>Asynchronously verifies cancelling the run cancels both provider calls.</summary>
    /// <returns>A task that represents the asynchronous operation.</returns>
    [Fact]
    public async Task CancellationStopsBothCalls()
    {
        var curatorEntered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var reviewerEntered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var curatorCancelled = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var reviewerCancelled = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var client = new ScriptedModelCompletionClient(
            (_, cancellationToken) => AwaitCancellationAsync(curatorEntered, curatorCancelled, cancellationToken),
            (_, cancellationToken) => AwaitCancellationAsync(reviewerEntered, reviewerCancelled, cancellationToken));
        using var repository = PipelineRepositories.CreateCommittedChange();
        await using var host = await AnalysisPipelineTestHost.CreateAsync(
            services => services.Replace(ServiceDescriptor.Scoped<IModelCompletionClient>(_ => client)));
        await host.StartAsync(TestContext.Current.CancellationToken);
        await host.OpenRepositoryAsync(repository.Path);
        var freshnessToken = await host.PrepareFreshnessTokenAsync(repository.Path, repository.DefaultTarget);
        var runId = await host.StartAsync(repository.Path, repository.DefaultTarget, freshnessToken);

        await Task.WhenAll(curatorEntered.Task, reviewerEntered.Task).WaitAsync(Deadline, TestContext.Current.CancellationToken);
        using var cancellation = await host.CancelAsync(runId);

        using var terminal = await host.PollUntilTerminalAsync(runId, Deadline);
        await Task.WhenAll(curatorCancelled.Task, reviewerCancelled.Task).WaitAsync(Deadline, TestContext.Current.CancellationToken);
        var result = terminal.RootElement.GetProperty("result");
        Assert.Equal("cancelled", result.GetProperty("state").GetString());
        Assert.Equal(JsonValueKind.Null, result.GetProperty("readingModel").ValueKind);
    }

    /// <summary>Asynchronously verifies a curator failure fails the run as before and cancels the reviewer instead of waiting for it.</summary>
    /// <returns>A task that represents the asynchronous operation.</returns>
    [Fact]
    public async Task CuratorFailureFailsTheRunAndCancelsTheReviewer()
    {
        var reviewerEntered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var reviewerCancelled = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var client = new ScriptedModelCompletionClient(
            async (_, cancellationToken) =>
            {
                await reviewerEntered.Task.WaitAsync(Deadline, cancellationToken);
                return Result.Fail<ModelCompletionModel>(
                    OperationError.ExternalDependencyFailure("provider unavailable", ModelCompletionErrorCode.ProviderUnavailable));
            },
            (_, cancellationToken) => AwaitCancellationAsync(reviewerEntered, reviewerCancelled, cancellationToken));

        using var terminal = await RunToTerminalAsync(client);

        var result = terminal.RootElement.GetProperty("result");
        Assert.Equal("failed", result.GetProperty("state").GetString());
        Assert.Equal("modelCompletion.providerUnavailable", result.GetProperty("terminal").GetProperty("failureCode").GetString());
        Assert.Equal(JsonValueKind.Null, result.GetProperty("readingModel").ValueKind);
        await reviewerCancelled.Task.WaitAsync(Deadline, TestContext.Current.CancellationToken);
    }

    private static async Task<Result<ModelCompletionModel>> AwaitCancellationAsync(
        TaskCompletionSource entered,
        TaskCompletionSource cancelled,
        CancellationToken cancellationToken)
    {
        entered.TrySetResult();
        try
        {
            await Task.Delay(Timeout.Infinite, cancellationToken);
        }
        catch (OperationCanceledException)
        {
            cancelled.TrySetResult();
            throw;
        }

        throw new InvalidOperationException("The awaiting responder resumed without a cancellation.");
    }

    private static async Task<JsonDocument> RunToTerminalAsync(
        ScriptedModelCompletionClient client,
        IReadOnlyDictionary<string, string?>? settings = null)
    {
        using var repository = PipelineRepositories.CreateCommittedChange();
        return await ScriptedPipelineRun.RunToTerminalAsync(repository, client, settings, Deadline);
    }
}
