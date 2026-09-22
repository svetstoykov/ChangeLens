using System.Text.Json;
using ChangeLens.Core.AnalysisRuns.Constants;
using ChangeLens.Core.AnalysisRuns.Models;
using ChangeLens.Core.Repositories.Models;
using ChangeLens.Core.Results.Models;
using ChangeLens.Core.Snapshots.Models;
using ChangeLens.Engine.AnalysisRuns.Constants;
using ChangeLens.Engine.AnalysisRuns.Handlers;
using ChangeLens.Engine.AnalysisRuns.Models;
using ChangeLens.Engine.IntegrationTests.Analysis.Handlers.Support;
using ChangeLens.Engine.IntegrationTests.Protocol.Support;
using ChangeLens.Engine.IntegrationTests.Support;
using ChangeLens.Engine.Protocol.Constants;
using ChangeLens.Engine.Protocol.Models;
using ChangeLens.Engine.Protocol.Services;
using Xunit;

namespace ChangeLens.Engine.IntegrationTests.Analysis.Handlers;

/// <summary>Verifies the analysis-pollRun protocol action mapping.</summary>
public sealed class AnalysisPollRunHandlerTests
{
    [Fact]
    public async Task MalformedRunIdReturnsUnknownRunWithoutCallingCoordinator()
    {
        var coordinator = new StubAnalysisRunCoordinator();
        var handler = new AnalysisPollRunHandler(
            coordinator,
            new StubEngineProtocolSerializer(new AnalysisPollRunParameters { RunId = "not-a-guid" }));

        var response = await handler.HandleAsync(CreateRequest(), TestContext.Current.CancellationToken);

        Assert.Equal("analysis.unknownRun", Assert.Single(Assert.IsType<ProtocolErrorResponse>(response).Errors).Code);
        Assert.False(coordinator.PollCalled);
    }

    [Fact]
    public async Task UnknownWellFormedRunReturnsUnknownRun()
    {
        var runId = Guid.NewGuid();
        var handler = new AnalysisPollRunHandler(
            new StubAnalysisRunCoordinator(pollRun: (_, _) => Task.FromResult<Result<AnalysisRunDetail>>(
                OperationError.NotFound("No analysis run matches the supplied identifier.", AnalysisErrorCode.UnknownRun))),
            new StubEngineProtocolSerializer(new AnalysisPollRunParameters { RunId = runId.ToString() }));

        var response = await handler.HandleAsync(CreateRequest(), TestContext.Current.CancellationToken);

        Assert.Equal("analysis.unknownRun", Assert.Single(Assert.IsType<ProtocolErrorResponse>(response).Errors).Code);
    }

    [Fact]
    public async Task PopulatedSummaryMapsCompletedWithLimitationsTerminal()
    {
        var detail = CreateDetail();
        var handler = new AnalysisPollRunHandler(
            new StubAnalysisRunCoordinator(pollRun: (_, _) => Task.FromResult<Result<AnalysisRunDetail>>(detail)),
            new StubEngineProtocolSerializer(new AnalysisPollRunParameters
            {
                RunId = detail.RunId.ToString(),
            }));

        var response = await handler.HandleAsync(CreateRequest(), TestContext.Current.CancellationToken);

        var summary = Assert.IsType<AnalysisRunSummaryResult>(Assert.IsType<ProtocolResultResponse<AnalysisRunSummaryResult>>(response).Result);
        var terminal = Assert.IsType<CompletedWithLimitationsAnalysisTerminalResult>(summary.Terminal);
        Assert.Equal(2, terminal.LimitationCount);
        Assert.Equal("completedWithLimitations", summary.State);
        Assert.Equal(detail.Repository.CanonicalPath, summary.Repository.CanonicalPath);
        Assert.Equal(detail.Comparison.TargetRevision, summary.Comparison.TargetRevision);
    }

    /// <summary>
    ///     Asynchronously projects snapshot identity and both capture facts for a captured run.
    /// </summary>
    /// <returns>A task that represents the asynchronous operation.</returns>
    [Fact]
    public async Task HandleAsyncCapturedRunProjectsSnapshotIdentityAndFacts()
    {
        var snapshotId = Guid.NewGuid();
        var detail = CapturedDetail(snapshotId, capturedChangedFileCount: 34,
            counts: new ExcludedUncommittedCounts(3, 2, 0, 1, 0));

        var result = await PollAsync(detail);

        Assert.Equal(snapshotId.ToString(), result.SnapshotId);
        Assert.Equal(2_000, result.CapturedAt);
        Assert.Collection(result.Facts,
            fact =>
            {
                Assert.Equal(AnalysisFactKind.ChangedFilesCaptured, fact.Kind);
                Assert.Equal(34, fact.Count);
                Assert.Null(fact.Detail);
            },
            fact =>
            {
                Assert.Equal(AnalysisFactKind.ExcludedUncommittedFiles, fact.Kind);
                Assert.Equal(3, fact.Count);
                Assert.Equal("2 staged, 1 untracked", fact.Detail);
            });
    }

    /// <summary>
    ///     Asynchronously omits the exclusion fact when nothing uncommitted was excluded.
    /// </summary>
    /// <returns>A task that represents the asynchronous operation.</returns>
    [Fact]
    public async Task HandleAsyncCapturedRunWithoutExclusionsEmitsOneFact()
    {
        var detail = CapturedDetail(Guid.NewGuid(), capturedChangedFileCount: 5,
            counts: new ExcludedUncommittedCounts(0, 0, 0, 0, 0));

        var result = await PollAsync(detail);

        Assert.Single(result.Facts);
        Assert.Equal(AnalysisFactKind.ChangedFilesCaptured, result.Facts[0].Kind);
    }

    /// <summary>
    ///     Asynchronously maps a completed run's stored reading projection onto the summary.
    /// </summary>
    /// <returns>A task that represents the asynchronous operation.</returns>
    [Fact]
    public async Task ProjectedCompletedRunMapsFocusOmissionsLimitationsAndRemoval()
    {
        var response = await PollWithRealSerializerAsync(CreateDetail(), CreateFixtureProjection());

        var summary = Assert.IsType<AnalysisRunSummaryResult>(
            Assert.IsType<ProtocolResultResponse<AnalysisRunSummaryResult>>(response).Result);
        var model = summary.ReadingModel!;
        var citation = Assert.Single(model.Citations);
        Assert.Collection(
            citation.Focus,
            range =>
            {
                Assert.Equal(12, range.StartLine);
                Assert.Equal(12, range.EndLine);
            },
            range =>
            {
                Assert.Equal(16, range.StartLine);
                Assert.Equal(17, range.EndLine);
            });
        var omission = Assert.Single(model.OmissionSummaries);
        Assert.Equal(1, omission.TotalCount);
        Assert.Equal(1, omission.SampleCount);
        Assert.Equal(1, omission.ResolvedSampleCount);
        var exclusion = Assert.Single(
            model.Limitations,
            limitation => limitation.Kind == ReadingModelProtocolConstants.LimitationUncommittedWorkExcluded);
        Assert.Null(exclusion.Path);
        var removal = Assert.Single(summary.ValidationRemovals!);
        Assert.Equal(ReadingModelProtocolConstants.RemovalScopeStatement, removal.Scope);
        Assert.Equal("thesis", removal.Id);
    }

    /// <summary>
    ///     Asynchronously verifies the real serializer round-trips the mapped citation focus ranges.
    /// </summary>
    /// <returns>A task that represents the asynchronous operation.</returns>
    [Fact]
    public async Task SerializedProjectedResponseRoundTripsCitationFocusOrder()
    {
        var response = await PollWithRealSerializerAsync(CreateDetail(), CreateFixtureProjection());

        var serialized = new EngineProtocolSerializer().SerializeResponse(response);

        Assert.True(serialized.IsSuccess);
        using var document = JsonDocument.Parse(serialized.Data!);
        var focus = document.RootElement
            .GetProperty("result")
            .GetProperty("readingModel")
            .GetProperty("citations")[0]
            .GetProperty("focus");
        Assert.Equal(2, focus.GetArrayLength());
    }

    /// <summary>
    ///     Asynchronously verifies an unreadable stored reading model fails with its stable error code.
    /// </summary>
    /// <returns>A task that represents the asynchronous operation.</returns>
    [Fact]
    public async Task UnreadableReadingModelProjectionReturnsUnreadableError()
    {
        var detail = CreateDetail() with
        {
            State = AnalysisRunState.Completed,
            Terminal = new AnalysisTerminalSummary(AnalysisTerminalKind.Completed, 1720000000500, null, null),
        };

        var response = await PollWithRealSerializerAsync(detail, new AnalysisReadingProjection("{not json", "[]"));

        Assert.Equal(
            AnalysisProtocolErrorCode.UnreadableReadingModel,
            Assert.Single(Assert.IsType<ProtocolErrorResponse>(response).Errors).Code);
    }

    /// <summary>
    ///     Asynchronously verifies a failed run maps captured facts without requesting a reading projection.
    /// </summary>
    /// <returns>A task that represents the asynchronous operation.</returns>
    [Fact]
    public async Task FailedRunMapsFactsWithoutRequestingReadingProjection()
    {
        var detail = CreateDetail() with
        {
            State = AnalysisRunState.Failed,
            CapturedAtUnixMilliseconds = 1720000000200,
            CapturedChangedFileCount = 7,
            CorrespondenceCandidateCount = 0,
            DisclosedEvidenceNodeCount = 3,
            ValidationRemovalCount = null,
            Terminal = new AnalysisTerminalSummary(
                AnalysisTerminalKind.Failed,
                1720000000500,
                null,
                "analysis.captureFailed"),
        };
        var projectionCalled = false;
        var handler = new AnalysisPollRunHandler(
            new StubAnalysisRunCoordinator(
                pollRun: (_, _) => Task.FromResult<Result<AnalysisRunDetail>>(detail),
                readingProjection: (_, _) =>
                {
                    projectionCalled = true;
                    return Task.FromResult(Result.Success<AnalysisReadingProjection?>(CreateFixtureProjection()));
                }),
            new StubEngineProtocolSerializer(new AnalysisPollRunParameters { RunId = detail.RunId.ToString() }));

        var response = await handler.HandleAsync(CreateRequest(), TestContext.Current.CancellationToken);

        var result = Assert.IsType<AnalysisRunSummaryResult>(
            Assert.IsType<ProtocolResultResponse<AnalysisRunSummaryResult>>(response).Result);
        Assert.Collection(
            result.Facts,
            fact =>
            {
                Assert.Equal(AnalysisFactKind.ChangedFilesCaptured, fact.Kind);
                Assert.Equal(7, fact.Count);
            },
            fact =>
            {
                Assert.Equal(AnalysisFactKind.CorrespondenceCandidates, fact.Kind);
                Assert.Equal(0, fact.Count);
            },
            fact =>
            {
                Assert.Equal(AnalysisFactKind.DisclosedEvidenceNodes, fact.Kind);
                Assert.Equal(3, fact.Count);
            });
        Assert.Null(result.ReadingModel);
        Assert.Null(result.ValidationRemovals);
        Assert.False(projectionCalled);
    }

    private static async Task<ProtocolResponse> PollWithRealSerializerAsync(
        AnalysisRunDetail detail,
        AnalysisReadingProjection? projection)
    {
        var coordinator = new StubAnalysisRunCoordinator(
            pollRun: (_, _) => Task.FromResult<Result<AnalysisRunDetail>>(detail),
            readingProjection: projection is null
                ? null
                : (_, _) => Task.FromResult(Result.Success<AnalysisReadingProjection?>(projection)));
        var handler = new AnalysisPollRunHandler(coordinator, new EngineProtocolSerializer());

        return await handler.HandleAsync(CreateDetailedRequest(detail.RunId), TestContext.Current.CancellationToken);
    }

    private static AnalysisReadingProjection CreateFixtureProjection()
    {
        using var fixture = JsonDocument.Parse(File.ReadAllText(Path.Combine(
            RepositoryPaths.EngineProtocolV1,
            "fixtures",
            "analysis-poll-run.completed-with-reading-model.result.json")));
        var result = fixture.RootElement.GetProperty("result");

        return new AnalysisReadingProjection(
            result.GetProperty("readingModel").GetRawText(),
            result.GetProperty("validationRemovals").GetRawText());
    }

    private static EngineProtocolRequest CreateDetailedRequest(Guid runId) => new()
    {
        ProtocolVersion = EngineProtocolConstants.CurrentVersion,
        RequestId = "analysis-poll-test",
        Action = AnalysisActionConstants.PollRunAction,
        Parameters = JsonSerializer.SerializeToElement(new { runId }),
    };

    private static async Task<AnalysisRunSummaryResult> PollAsync(AnalysisRunDetail detail)
    {
        var handler = new AnalysisPollRunHandler(
            new StubAnalysisRunCoordinator(pollRun: (_, _) => Task.FromResult<Result<AnalysisRunDetail>>(detail)),
            new StubEngineProtocolSerializer(new AnalysisPollRunParameters { RunId = detail.RunId.ToString() }));

        var response = await handler.HandleAsync(CreateRequest(), TestContext.Current.CancellationToken);

        return Assert.IsType<AnalysisRunSummaryResult>(Assert.IsType<ProtocolResultResponse<AnalysisRunSummaryResult>>(response).Result);
    }

    private static AnalysisRunDetail CapturedDetail(Guid snapshotId, int capturedChangedFileCount, ExcludedUncommittedCounts counts) =>
        CreateDetail() with
        {
            CapturedAtUnixMilliseconds = 2_000,
            SnapshotId = snapshotId,
            ManifestHash = new string('a', 64),
            CapturedChangedFileCount = capturedChangedFileCount,
            ExcludedUncommittedCounts = counts,
        };

    private static AnalysisRunDetail CreateDetail() => new(
        Guid.Parse("0198a1b2-3c4d-4e5f-8a9b-0123456789ab"),
        AnalysisRunState.CompletedWithLimitations,
        new AnalysisRepositoryIdentity(
            Guid.Parse("5298a1b2-3c4d-4e5f-8a9b-0123456789ab"),
            "change_lens",
            "/projects/change_lens",
            "/projects/change_lens",
            "0123456789abcdef0123456789abcdef01234567"),
        new AnalysisComparisonIdentity(
            "refs/heads/feature/comparison",
            "89abcdef0123456789abcdef0123456789abcdef",
            new string('0', 64)),
            null,
        1720000000000,
        1720000000100,
        null,
        null,
        null,
        null,
        null,
        null,
        null,
        null,
        false,
        new AnalysisTerminalSummary(AnalysisTerminalKind.CompletedWithLimitations, 1720000000500, 2, null),
        null,
        null);

    private static EngineProtocolRequest CreateRequest() => new()
    {
        ProtocolVersion = EngineProtocolConstants.CurrentVersion,
        RequestId = "analysis-poll-test",
        Action = AnalysisActionConstants.PollRunAction,
        Parameters = JsonSerializer.SerializeToElement(new { runId = "ignored" }),
    };
}
