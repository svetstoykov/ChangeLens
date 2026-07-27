using System.Text.Json;
using ChangeLens.Core.Comparisons.Services;
using ChangeLens.Core.Results.Models;
using ChangeLens.Engine.Comparisons.Models;
using ChangeLens.Engine.Comparisons.Services;
using ChangeLens.Engine.Protocol.Models;
using ChangeLens.Engine.Protocol.Services;
using ChangeLens.Engine.Preferences.Services;
using ChangeLens.Engine.Repositories.Models;
using ChangeLens.Engine.Repositories.Services;
using ChangeLens.Engine.UnitTests.Comparisons.Support;
using ChangeLens.Engine.UnitTests.EngineStatus.Support;
using ChangeLens.Engine.UnitTests.Repositories.Support;
using ChangeLens.Engine.UnitTests.Support;
using Xunit;

namespace ChangeLens.Engine.UnitTests.Protocol;

/// <summary>
///     Verifies typed action selection and Core Result mapping.
/// </summary>
public sealed class EngineActionProcessorTests
{
    private const string Revision = "0123456789abcdef0123456789abcdef01234567";
    private const string Token =
        "0123456789abcdef0123456789abcdef0123456789abcdef0123456789abcdef";

    /// <summary>
    ///     Verifies that status success returns a correlated payload-free result.
    /// </summary>
    [Fact]
    public async Task ProcessAsyncReturnsPayloadFreeStatusResult()
    {
        var processor = CreateProcessor(_ => Task.FromResult(Result.Success()));

        var response = await processor.ProcessAsync(
            CreateRequest(),
            TestContext.Current.CancellationToken);

        var result = Assert.IsType<ProtocolResultResponse<JsonElement?>>(response);
        Assert.Equal("request-1", result.RequestId);
        Assert.Null(result.Result);
    }

    /// <summary>
    ///     Verifies that status ignores every supplied parameters value.
    /// </summary>
    /// <param name="parametersJson">The explicitly supplied parameters value.</param>
    [Theory]
    [InlineData("null")]
    [InlineData("{}")]
    [InlineData("[]")]
    [InlineData("false")]
    [InlineData("1")]
    [InlineData("\"value\"")]
    public async Task ProcessAsyncIgnoresEverySuppliedStatusParametersValue(string parametersJson)
    {
        var callCount = 0;
        var processor = CreateProcessor(
            _ =>
            {
                callCount++;
                return Task.FromResult(Result.Success());
            });

        var response = await processor.ProcessAsync(
            CreateRequest(parameters: Parse(parametersJson)),
            TestContext.Current.CancellationToken);

        Assert.IsNotType<ProtocolErrorResponse>(response);
        Assert.Equal(1, callCount);
    }

    /// <summary>
    ///     Verifies that other inputless actions ignore supplied parameters.
    /// </summary>
    /// <param name="action">The inputless action to process.</param>
    /// <param name="parametersJson">The explicitly supplied parameters value.</param>
    [Theory]
    [InlineData("repositories.restoreLast", "{}")]
    [InlineData("repositories.listRecent", "null")]
    [InlineData("preferences.getColorTheme", "[]")]
    public async Task ProcessAsyncIgnoresSuppliedParametersForInputlessAction(
        string action,
        string parametersJson)
    {
        var processor = CreateProcessor();

        var response = await processor.ProcessAsync(
            CreateRequest(action: action, parameters: Parse(parametersJson)),
            TestContext.Current.CancellationToken);

        Assert.IsNotType<ProtocolErrorResponse>(response);
    }

    /// <summary>
    ///     Verifies that unsupported versions return the stable unprocessable-input error.
    /// </summary>
    [Fact]
    public async Task ProcessAsyncRejectsUnsupportedVersion()
    {
        var processor = CreateProcessor(_ => Task.FromResult(Result.Success()));

        var response = await processor.ProcessAsync(
            CreateRequest(protocolVersion: 2),
            TestContext.Current.CancellationToken);

        var error = Assert.Single(Assert.IsType<ProtocolErrorResponse>(response).Errors);
        Assert.Equal(ErrorType.UnprocessableInput, error.Type);
        Assert.Equal("protocol.unsupportedVersion", error.Code);
    }

    /// <summary>
    ///     Verifies that an unknown action returns the stable not-found error.
    /// </summary>
    [Fact]
    public async Task ProcessAsyncRejectsUnknownAction()
    {
        var processor = CreateProcessor(_ => Task.FromResult(Result.Success()));

        var response = await processor.ProcessAsync(
            CreateRequest(action: "analysis.run"),
            TestContext.Current.CancellationToken);

        var error = Assert.Single(Assert.IsType<ProtocolErrorResponse>(response).Errors);
        Assert.Equal(ErrorType.NotFound, error.Type);
        Assert.Equal("protocol.unknownAction", error.Code);
    }

    /// <summary>
    ///     Verifies that a known Core failure is preserved at the protocol boundary.
    /// </summary>
    [Fact]
    public async Task ProcessAsyncPreservesCoreFailure()
    {
        var sourceError = OperationError.Conflict("Status conflict.", "status.conflict");
        var processor = CreateProcessor(_ => Task.FromResult(Result.Fail(sourceError)));

        var response = await processor.ProcessAsync(
            CreateRequest(),
            TestContext.Current.CancellationToken);

        var error = Assert.Single(Assert.IsType<ProtocolErrorResponse>(response).Errors);
        Assert.Equal(sourceError.Type, error.Type);
        Assert.Equal(sourceError.Code, error.Code);
        Assert.Equal(sourceError.Message, error.Message);
    }

    /// <summary>
    ///     Verifies that unexpected capability exceptions are logged once and sanitized.
    /// </summary>
    [Fact]
    public async Task ProcessAsyncSanitizesUnexpectedCapabilityException()
    {
        var logger = new TestLogger<EngineActionProcessor>();
        var processor = CreateProcessor(
            _ => throw new InvalidOperationException("sensitive capability detail"),
            logger);

        var response = await processor.ProcessAsync(
            CreateRequest(),
            TestContext.Current.CancellationToken);

        var error = Assert.Single(Assert.IsType<ProtocolErrorResponse>(response).Errors);
        Assert.Equal(ErrorType.InternalError, error.Type);
        Assert.Equal("engine.unexpectedFailure", error.Code);
        Assert.DoesNotContain("sensitive", error.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(1, logger.ErrorCount);
        Assert.IsType<InvalidOperationException>(logger.LastException);
    }

    /// <summary>
    ///     Verifies that capability cancellation from the supplied token remains exception-based.
    /// </summary>
    [Fact]
    public async Task ProcessAsyncPreservesCancellation()
    {
        using var source = new CancellationTokenSource();
        source.Cancel();
        var processor = CreateProcessor(
            token => Task.FromCanceled<Result>(token));

        await Assert.ThrowsAsync<TaskCanceledException>(
            () => processor.ProcessAsync(CreateRequest(), source.Token));
    }

    /// <summary>
    ///     Verifies that repository open rejects parameters that do not match its strict schema before Core I/O.
    /// </summary>
    /// <param name="parametersJson">
    ///     The malformed parameters JSON, or <see langword="null" /> when parameters are omitted.
    /// </param>
    [Theory]
    [InlineData(null)]
    [InlineData("null")]
    [InlineData("[]")]
    [InlineData("false")]
    [InlineData("1")]
    [InlineData("\"value\"")]
    [InlineData("{}")]
    [InlineData("""{"extra":true}""")]
    [InlineData("""{"path":null}""")]
    [InlineData("""{"path":1}""")]
    [InlineData("""{"Path":"/selected"}""")]
    [InlineData("""{"path":"/first","path":"/second"}""")]
    public async Task ProcessAsyncRejectsMalformedRepositoryParametersBeforeCoreIo(string? parametersJson)
    {
        var fixture = new RepositoryInspectorFixture();
        var processor = CreateProcessor(fixture: fixture);

        var response = await processor.ProcessAsync(
            CreateRequest(
                action: "repositories.open",
                parameters: parametersJson is null ? default : Parse(parametersJson)),
            TestContext.Current.CancellationToken);

        var error = Assert.Single(Assert.IsType<ProtocolErrorResponse>(response).Errors);
        Assert.Equal(ErrorType.Validation, error.Type);
        Assert.Equal("protocol.invalidRequest", error.Code);
        Assert.Empty(fixture.Paths);
        Assert.Empty(fixture.Commands);
    }

    /// <summary>
    ///     Verifies that structurally valid path strings are validated by Core rather than the common envelope.
    /// </summary>
    /// <param name="path">The path value rejected by Core.</param>
    [Theory]
    [InlineData(" \t\r\n")]
    [InlineData("/repository\u0000child")]
    public async Task ProcessAsyncLetsCoreRejectStructurallyValidRepositoryPaths(string path)
    {
        var fixture = new RepositoryInspectorFixture();
        var processor = CreateProcessor(fixture: fixture);

        var response = await processor.ProcessAsync(
            CreateRepositoryRequest(path),
            TestContext.Current.CancellationToken);

        var error = Assert.Single(Assert.IsType<ProtocolErrorResponse>(response).Errors);
        Assert.Equal(ErrorType.Validation, error.Type);
        Assert.Equal("repository.invalidPath", error.Code);
        Assert.Empty(fixture.Paths);
        Assert.Empty(fixture.Commands);
    }

    /// <summary>
    ///     Verifies that an overlong structurally bound path is rejected by Core.
    /// </summary>
    [Fact]
    public async Task ProcessAsyncLetsCoreRejectPathLongerThan8192Scalars()
    {
        var fixture = new RepositoryInspectorFixture();
        var processor = CreateProcessor(fixture: fixture);

        var response = await processor.ProcessAsync(
            CreateRepositoryRequest(new string('a', 8_193)),
            TestContext.Current.CancellationToken);

        var error = Assert.Single(Assert.IsType<ProtocolErrorResponse>(response).Errors);
        Assert.Equal(ErrorType.Validation, error.Type);
        Assert.Equal("repository.invalidPath", error.Code);
        Assert.Empty(fixture.Paths);
        Assert.Empty(fixture.Commands);
    }

    /// <summary>
    ///     Verifies that attached repository state maps to the exact typed protocol result.
    /// </summary>
    [Fact]
    public async Task ProcessAsyncMapsBranchRepositoryResult()
    {
        var fixture = new RepositoryInspectorFixture();
        fixture.EnqueueSuccessfulInspection(Revision, "main");
        var processor = CreateProcessor(fixture: fixture);

        var response = await processor.ProcessAsync(
            CreateRepositoryRequest("/selected"),
            TestContext.Current.CancellationToken);

        var result = Assert.IsType<ProtocolResultResponse<RepositoryOpenResult>>(response);
        Assert.Equal(
            new RepositoryOpenResult(
                Guid.Parse("01234567-89ab-cdef-0123-456789abcdef"),
                new RepositoryResult(
                    "change_lens",
                    "/projects/change_lens",
                    new BranchRepositoryHeadResult("main", Revision)),
                null),
            result.Result);
    }

    /// <summary>
    ///     Verifies that detached repository state maps to the exact typed protocol result.
    /// </summary>
    [Fact]
    public async Task ProcessAsyncMapsDetachedRepositoryResult()
    {
        var fixture = new RepositoryInspectorFixture();
        fixture.EnqueueSuccessfulInspection(Revision, branchName: null);
        var processor = CreateProcessor(fixture: fixture);

        var response = await processor.ProcessAsync(
            CreateRepositoryRequest("/selected"),
            TestContext.Current.CancellationToken);

        var result = Assert.IsType<ProtocolResultResponse<RepositoryOpenResult>>(response);
        Assert.Equal(
            new RepositoryOpenResult(
                Guid.Parse("01234567-89ab-cdef-0123-456789abcdef"),
                new RepositoryResult(
                    "change_lens",
                    "/projects/change_lens",
                    new DetachedRepositoryHeadResult(Revision)),
                null),
            result.Result);
    }

    /// <summary>
    ///     Verifies that a Core repository failure keeps its category, code, and message.
    /// </summary>
    [Fact]
    public async Task ProcessAsyncPreservesRepositoryCoreFailure()
    {
        var sourceError = OperationError.Unauthorized(
            "Repository access was denied.",
            "repository.accessDenied");
        var fixture = new RepositoryInspectorFixture();
        fixture.EnqueuePath(Result.Fail<string>(sourceError));
        var processor = CreateProcessor(fixture: fixture);

        var response = await processor.ProcessAsync(
            CreateRepositoryRequest("/selected"),
            TestContext.Current.CancellationToken);

        var error = Assert.Single(Assert.IsType<ProtocolErrorResponse>(response).Errors);
        Assert.Equal(sourceError.Type, error.Type);
        Assert.Equal(sourceError.Code, error.Code);
        Assert.Equal(sourceError.Message, error.Message);
    }

    /// <summary>
    ///     Verifies that repository cancellation remains exception-based at the Engine boundary.
    /// </summary>
    [Fact]
    public async Task ProcessAsyncPreservesRepositoryCancellation()
    {
        using var source = new CancellationTokenSource();
        source.Cancel();
        var fixture = new RepositoryInspectorFixture();
        fixture.EnqueuePath(
            (_, token) => Task.FromCanceled<Result<string>>(token));
        var processor = CreateProcessor(fixture: fixture);

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => processor.ProcessAsync(CreateRepositoryRequest("/selected"), source.Token));
    }

    /// <summary>
    ///     Verifies that unexpected repository collaborator failures are logged once and sanitized.
    /// </summary>
    /// <param name="throwFromRunner">
    ///     <see langword="true" /> to throw from Git execution; otherwise, throw from path resolution.
    /// </param>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ProcessAsyncSanitizesUnexpectedRepositoryException(bool throwFromRunner)
    {
        var fixture = new RepositoryInspectorFixture();
        if (throwFromRunner)
        {
            fixture.EnqueuePath(Result.Success<string>("/physical/selection"));
            fixture.EnqueueCommand(
                (_, _) => throw new InvalidOperationException("sensitive Git detail"));
        }
        else
        {
            fixture.EnqueuePath(
                (_, _) => throw new InvalidOperationException("sensitive path detail"));
        }

        var logger = new TestLogger<EngineActionProcessor>();
        var processor = CreateProcessor(fixture: fixture, logger: logger);

        var response = await processor.ProcessAsync(
            CreateRepositoryRequest("/selected"),
            TestContext.Current.CancellationToken);

        var error = Assert.Single(Assert.IsType<ProtocolErrorResponse>(response).Errors);
        Assert.Equal(ErrorType.InternalError, error.Type);
        Assert.Equal("engine.unexpectedFailure", error.Code);
        Assert.DoesNotContain("sensitive", error.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(1, logger.ErrorCount);
        Assert.IsType<InvalidOperationException>(logger.LastException);
    }

    /// <summary>
    ///     Verifies malformed comparison parameters are rejected before repository or Git access.
    /// </summary>
    /// <param name="action">The fixed comparison action.</param>
    /// <param name="parametersJson">
    ///     The malformed parameters JSON, or <see langword="null" /> when parameters are omitted.
    /// </param>
    [Theory]
    [InlineData("comparisons.listTargets", null)]
    [InlineData("comparisons.listTargets", "null")]
    [InlineData("comparisons.listTargets", "{}")]
    [InlineData("comparisons.listTargets", """{"path":null}""")]
    [InlineData("comparisons.listTargets", """{"Path":"/selected"}""")]
    [InlineData("comparisons.listTargets", """{"path":"/selected","query":null}""")]
    [InlineData("comparisons.listTargets", """{"path":"/selected","after":null}""")]
    [InlineData(
        "comparisons.listTargets",
        """{"path":"/selected","targetSetToken":null}""")]
    [InlineData("comparisons.listTargets", """{"path":"/selected","extra":true}""")]
    [InlineData("comparisons.listTargets", """{"path":"/first","path":"/second"}""")]
    [InlineData("comparisons.prepare", null)]
    [InlineData("comparisons.prepare", "[]")]
    [InlineData("comparisons.prepare", """{"path":"/selected"}""")]
    [InlineData("comparisons.prepare", """{"path":"/selected","target":null}""")]
    [InlineData("comparisons.prepare", """{"path":"/selected","Target":"refs/heads/topic"}""")]
    [InlineData(
        "comparisons.prepare",
        """{"path":"/selected","target":"refs/heads/topic","extra":true}""")]
    [InlineData("comparisons.checkFreshness", null)]
    [InlineData("comparisons.checkFreshness", "false")]
    [InlineData(
        "comparisons.checkFreshness",
        """{"path":"/selected","target":"refs/heads/topic"}""")]
    [InlineData(
        "comparisons.checkFreshness",
        """{"path":"/selected","target":"refs/heads/topic","freshnessToken":null}""")]
    [InlineData(
        "comparisons.checkFreshness",
        """{"path":"/selected","target":"refs/heads/topic","FreshnessToken":"0123456789abcdef0123456789abcdef0123456789abcdef0123456789abcdef"}""")]
    public async Task ProcessAsyncRejectsMalformedComparisonParametersBeforeCoreIo(
        string action,
        string? parametersJson)
    {
        var comparison = new ComparisonProcessorFixture();
        var processor = CreateProcessor(comparisonFixture: comparison);

        var response = await processor.ProcessAsync(
            CreateRequest(
                action: action,
                parameters: parametersJson is null ? default : Parse(parametersJson)),
            TestContext.Current.CancellationToken);

        var error = Assert.Single(Assert.IsType<ProtocolErrorResponse>(response).Errors);
        Assert.Equal(ErrorType.Validation, error.Type);
        Assert.Equal("protocol.invalidRequest", error.Code);
        Assert.Empty(comparison.Paths);
        Assert.Empty(comparison.Commands);
    }

    /// <summary>
    ///     Verifies target discovery receives the exact bound values and maps a concrete target-page result.
    /// </summary>
    [Fact]
    public async Task ProcessAsyncDispatchesListTargetsAndMapsConcretePage()
    {
        var comparison = new ComparisonProcessorFixture();
        comparison.EnqueueInspection();
        comparison.EnqueueTargets(
            ComparisonProcessorFixture.Target(
                "refs/heads/topic",
                ComparisonProcessorFixture.TargetRevision),
            ComparisonProcessorFixture.Target("refs/remotes/origin/main"));
        var processor = CreateProcessor(comparisonFixture: comparison);

        var response = await processor.ProcessAsync(
            CreateRequest(
                action: "comparisons.listTargets",
                parameters: Parse("""{"path":"/selected","query":"topic"}""")),
            TestContext.Current.CancellationToken);

        var result = Assert.IsType<ProtocolResultResponse<ComparisonTargetPageResult>>(response);
        var target = Assert.Single(result.Result!.Targets);
        Assert.Equal(ComparisonTargetKindResult.Local, target.Kind);
        Assert.Equal("topic", target.Name);
        Assert.Equal("refs/heads/topic", target.FullName);
        Assert.Equal(ComparisonProcessorFixture.TargetRevision, target.Revision);
        Assert.Equal("/selected", comparison.Paths[0]);
        Assert.Equal(2, comparison.Paths.Count);
        Assert.Contains(
            comparison.Commands,
            command => command.Arguments.Contains("for-each-ref", StringComparer.Ordinal));
    }

    /// <summary>
    ///     Verifies comparison preparation receives the exact target and maps repository and readiness facts.
    /// </summary>
    [Fact]
    public async Task ProcessAsyncDispatchesPrepareAndReusesRepositoryMapper()
    {
        var comparison = new ComparisonProcessorFixture();
        comparison.EnqueuePreparation();
        var processor = CreateProcessor(comparisonFixture: comparison);

        var response = await processor.ProcessAsync(
            CreateRequest(
                action: "comparisons.prepare",
                parameters: Parse(
                    """{"path":"/selected","target":"refs/heads/topic"}""")),
            TestContext.Current.CancellationToken);

        var result = Assert.IsType<ProtocolResultResponse<ComparisonPrepareResult>>(response);
        Assert.Equal(
            RepositoryResult.FromDescriptor(
                new ChangeLens.Core.Repositories.Models.RepositoryDescriptor(
                    "change_lens",
                    ComparisonProcessorFixture.CanonicalPath,
                    new ChangeLens.Core.Repositories.Models.BranchRepositoryHead(
                        "main",
                        ComparisonProcessorFixture.HeadRevision))),
            result.Result!.Repository);
        Assert.Equal("refs/heads/topic", result.Result.Target.FullName);
        Assert.Equal(ComparisonProcessorFixture.MergeBaseRevision, result.Result.MergeBaseRevision);
        Assert.Equal(5, result.Result.CurrentWorkCommitCount);
        Assert.Equal(3, result.Result.TargetOnlyCommitCount);
        Assert.Equal(1, result.Result.UncommittedFileTotal);
        Assert.IsType<ReadyComparisonReadinessResult>(result.Result.Readiness);
    }

    /// <summary>
    ///     Verifies freshness checking receives the exact target and token and maps the tagged state.
    /// </summary>
    [Fact]
    public async Task ProcessAsyncDispatchesCheckFreshnessAndMapsTaggedState()
    {
        var comparison = new ComparisonProcessorFixture();
        comparison.EnqueueFreshnessCheck();
        var processor = CreateProcessor(comparisonFixture: comparison);

        var response = await processor.ProcessAsync(
            CreateRequest(
                action: "comparisons.checkFreshness",
                parameters: Parse(
                    $$"""{"path":"/selected","target":"refs/heads/topic","freshnessToken":"{{Token}}"}""")),
            TestContext.Current.CancellationToken);

        var result = Assert.IsType<ProtocolResultResponse<ComparisonFreshnessResult>>(response);
        Assert.IsType<StaleComparisonFreshnessResult>(result.Result);
        Assert.Equal("/selected", comparison.Paths[0]);
        Assert.Equal(2, comparison.Paths.Count);
        Assert.Contains(
            comparison.Commands,
            command => command.Arguments.Contains("for-each-ref", StringComparer.Ordinal));
    }

    /// <summary>
    ///     Verifies comparison Core failures retain category, code, message, and request correlation.
    /// </summary>
    [Fact]
    public async Task ProcessAsyncPreservesComparisonCoreFailure()
    {
        var sourceError = OperationError.Unauthorized(
            "Repository access was denied.",
            "repository.accessDenied");
        var comparison = new ComparisonProcessorFixture();
        comparison.EnqueuePath((_, _) => Task.FromResult(Result.Fail<string>(sourceError)));
        var processor = CreateProcessor(comparisonFixture: comparison);

        var response = await processor.ProcessAsync(
            CreateRequest(
                action: "comparisons.listTargets",
                parameters: Parse("""{"path":"/selected"}""")),
            TestContext.Current.CancellationToken);

        var errorResponse = Assert.IsType<ProtocolErrorResponse>(response);
        Assert.Equal("request-1", errorResponse.RequestId);
        var error = Assert.Single(errorResponse.Errors);
        Assert.Equal(sourceError.Type, error.Type);
        Assert.Equal(sourceError.Code, error.Code);
        Assert.Equal(sourceError.Message, error.Message);
    }

    /// <summary>
    ///     Verifies caller cancellation remains exception-based for every comparison action.
    /// </summary>
    /// <param name="action">The fixed comparison action.</param>
    /// <param name="parametersJson">The valid comparison parameters.</param>
    [Theory]
    [InlineData("comparisons.listTargets", """{"path":"/selected"}""")]
    [InlineData(
        "comparisons.prepare",
        """{"path":"/selected","target":"refs/heads/topic"}""")]
    [InlineData(
        "comparisons.checkFreshness",
        """{"path":"/selected","target":"refs/heads/topic","freshnessToken":"0123456789abcdef0123456789abcdef0123456789abcdef0123456789abcdef"}""")]
    public async Task ProcessAsyncPreservesComparisonCancellation(
        string action,
        string parametersJson)
    {
        using var source = new CancellationTokenSource();
        source.Cancel();
        var processor = CreateProcessor(comparisonFixture: new ComparisonProcessorFixture());

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => processor.ProcessAsync(
                CreateRequest(action: action, parameters: Parse(parametersJson)),
                source.Token));
    }

    /// <summary>
    ///     Verifies unexpected comparison failures are logged once and sanitized without logging parameters.
    /// </summary>
    [Fact]
    public async Task ProcessAsyncSanitizesUnexpectedComparisonException()
    {
        const string sensitivePath = "/sensitive/comparison/path";
        var comparison = new ComparisonProcessorFixture();
        comparison.EnqueuePath(
            (_, _) => throw new InvalidOperationException("sensitive comparison detail"));
        var logger = new TestLogger<EngineActionProcessor>();
        var processor = CreateProcessor(logger: logger, comparisonFixture: comparison);

        var response = await processor.ProcessAsync(
            CreateRequest(
                action: "comparisons.listTargets",
                parameters: Parse(
                    $$"""{"path":"{{sensitivePath}}","query":"sensitive-query"}""")),
            TestContext.Current.CancellationToken);

        var error = Assert.Single(Assert.IsType<ProtocolErrorResponse>(response).Errors);
        Assert.Equal(ErrorType.InternalError, error.Type);
        Assert.Equal("engine.unexpectedFailure", error.Code);
        Assert.DoesNotContain("sensitive", error.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(1, logger.ErrorCount);
        Assert.DoesNotContain(
            "sensitive",
            logger.LastException!.Message,
            StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain(
            logger.Entries,
            entry => entry.Contains(sensitivePath, StringComparison.Ordinal) ||
                     entry.Contains("sensitive-query", StringComparison.Ordinal));
    }

    /// <summary>
    ///     Verifies comparison actions remain explicit switch branches without dynamic action infrastructure.
    /// </summary>
    [Fact]
    public void ProcessorSourceKeepsThreeExplicitComparisonBranches()
    {
        var source = File.ReadAllText(
            Path.Combine(
                FindRepositoryRoot(),
                "src",
                "engine",
                "ChangeLens.Engine",
                "Protocol",
                "Services",
                "EngineActionProcessor.cs"));

        Assert.Contains("ComparisonActionConstants.ListTargetsAction =>", source, StringComparison.Ordinal);
        Assert.Contains("ComparisonActionConstants.PrepareAction =>", source, StringComparison.Ordinal);
        Assert.Contains("ComparisonActionConstants.CheckFreshnessAction =>", source, StringComparison.Ordinal);
        Assert.DoesNotContain("IMediator", source, StringComparison.Ordinal);
        Assert.DoesNotContain("AddKeyed", source, StringComparison.Ordinal);
        Assert.DoesNotContain("Activator.", source, StringComparison.Ordinal);
        Assert.DoesNotContain("GetType()", source, StringComparison.Ordinal);
        Assert.DoesNotContain("Dictionary<string", source, StringComparison.Ordinal);
        Assert.DoesNotContain("actionRegistry", source, StringComparison.OrdinalIgnoreCase);
    }

    private static EngineActionProcessor CreateProcessor(
        Func<CancellationToken, Task<Result>>? checkStatusAsync = null,
        TestLogger<EngineActionProcessor>? logger = null,
        RepositoryInspectorFixture? fixture = null,
        ComparisonProcessorFixture? comparisonFixture = null)
    {
        fixture ??= new RepositoryInspectorFixture();
        var serializer = new EngineProtocolSerializer();
        var targetDiscovery = comparisonFixture?.TargetDiscovery ??
                              new ChangeLens.Core.Comparisons.Services.GitComparisonTargetDiscovery(
                                  fixture.Inspector,
                                  fixture);
        var preparer = comparisonFixture?.Preparer ??
                       new ChangeLens.Core.Comparisons.Services.GitComparisonPreparer(
                           fixture.Inspector,
                           targetDiscovery,
                           fixture,
                           new ComparisonFileSummaryComposer());
        var freshnessChecker = comparisonFixture?.FreshnessChecker ??
                               new ChangeLens.Core.Comparisons.Services.GitComparisonFreshnessChecker(
                                   fixture.Inspector,
                                   targetDiscovery,
                                   fixture);
        var remoteBaselineTracker = comparisonFixture?.RemoteBaselineTracker ??
                                     new ChangeLens.Core.Git.Services.GitRemoteBaselineTracker(
                                         fixture.Inspector,
                                         fixture);
        return new EngineActionProcessor(
            new StubEngineStatusService(checkStatusAsync ?? (_ => Task.FromResult(Result.Success()))),
            new RepositoryHistoryService(
                comparisonFixture?.RepositoryInspector ?? fixture.Inspector,
                new StubRepositoryHistoryStore(),
                new StubCanonicalRepositoryPathKeyProvider(),
                TimeProvider.System),
            new ColorThemePreferenceService(new StubColorThemePreferenceStore()),
            targetDiscovery,
            preparer,
            freshnessChecker,
            remoteBaselineTracker,
            new ComparisonTargetPageBuilder(serializer),
            serializer,
            logger ?? new TestLogger<EngineActionProcessor>());
    }

    private static EngineProtocolRequest CreateRepositoryRequest(string path) =>
        CreateRequest(
            action: "repositories.open",
            parameters: Parse(JsonSerializer.Serialize(new { path })));

    private static EngineProtocolRequest CreateRequest(
        int protocolVersion = 1,
        string action = "engine.checkStatus",
        JsonElement parameters = default) =>
        new()
        {
            ProtocolVersion = protocolVersion,
            RequestId = "request-1",
            Action = action,
            Parameters = parameters,
        };

    private static JsonElement Parse(string json)
    {
        using var document = JsonDocument.Parse(json);
        return document.RootElement.Clone();
    }

    private static string FindRepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);

        while (directory is not null)
        {
            if (File.Exists(Path.Combine(directory.FullName, "src", "engine", "ChangeLens.slnx")))
            {
                return directory.FullName;
            }

            directory = directory.Parent;
        }

        throw new DirectoryNotFoundException("The ChangeLens repository root could not be located.");
    }
}
