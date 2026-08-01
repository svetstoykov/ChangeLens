using System.Text.Json;
using ChangeLens.Core.AnalysisRuns.Interfaces;
using ChangeLens.Core.AnalysisRuns.Models;
using ChangeLens.Core.Comparisons.Interfaces;
using ChangeLens.Core.Git.Interfaces;
using ChangeLens.Core.LocalState.Interfaces;
using ChangeLens.Engine.AnalysisRuns.Interfaces;
using ChangeLens.Engine.Hosting.Extensions;
using ChangeLens.Engine.Hosting.Services;
using ChangeLens.Engine.IntegrationTests.Support;
using ChangeLens.Engine.Logging.Constants;
using ChangeLens.Engine.Logging.Extensions;
using ChangeLens.Engine.Protocol.Constants;
using ChangeLens.Engine.Protocol.Interfaces;
using ChangeLens.Engine.Protocol.Models;
using ChangeLens.Engine.Protocol.Services;
using ChangeLens.Infrastructure.FileSystem.Services;
using ChangeLens.Infrastructure.LocalState.Constants;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using Xunit;

namespace ChangeLens.Engine.IntegrationTests.Hosting.Support;

/// <summary>
///     Builds a production-composed engine host with controlled local state for analysis processor tests.
/// </summary>
internal sealed class EngineHostTestFixture : IAsyncDisposable
{
    private readonly TemporaryDirectory? _temporaryDirectory;
    private readonly BlockingProtocolTransport _protocolTransport;
    private readonly string _repositoryPath;
    private IHost? _host;
    private TaskCompletionSource? _blockingPipelineRelease;
    private TaskCompletionSource? _pipelineStarted;
    private Guid _repositoryId;

    private EngineHostTestFixture(
        IHost host,
        TemporaryDirectory? temporaryDirectory,
        BlockingProtocolTransport protocolTransport,
        string repositoryPath)
    {
        this._host = host;
        this._temporaryDirectory = temporaryDirectory;
        this._protocolTransport = protocolTransport;
        this._repositoryPath = repositoryPath;
    }

    /// <summary>
    ///     Gets the initialized production-composed engine host.
    /// </summary>
    internal IHost Host => this._host ?? throw new ObjectDisposedException(nameof(EngineHostTestFixture));

    /// <summary>
    ///     Gets the retained identifier for the fixture's default repository.
    /// </summary>
    internal Guid RepositoryId => this._repositoryId;

    /// <summary>
    ///     Asynchronously creates an initialized engine host over a new temporary local-state directory.
    /// </summary>
    /// <returns>A task whose result contains the initialized hosting fixture.</returns>
    internal static async Task<EngineHostTestFixture> CreateAsync()
    {
        return await CreateWithTemporaryDirectoryAsync(null);
    }

    /// <summary>
    ///     Asynchronously creates an initialized engine host whose first pipeline invocation throws.
    /// </summary>
    /// <returns>A task whose result contains the initialized hosting fixture.</returns>
    internal static async Task<EngineHostTestFixture> CreateFailingFirstRunAsync()
    {
        var invocationCount = 0;
        return await CreateWithTemporaryDirectoryAsync(
            pipelineRun: (_, _, _) =>
            {
                if (Interlocked.Increment(ref invocationCount) == 1)
                {
                    throw new InvalidOperationException("The controlled first pipeline invocation failed.");
                }

                return Task.CompletedTask;
            },
            completeSuccessfulRuns: true);
    }

    /// <summary>
    ///     Asynchronously creates an initialized engine host whose pipeline blocks until released.
    /// </summary>
    /// <returns>A task whose result contains the initialized hosting fixture.</returns>
    internal static async Task<EngineHostTestFixture> CreateBlockingPipelineAsync()
    {
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var fixture = await CreateWithTemporaryDirectoryAsync(
            (_, _, shutdownToken) => release.Task.WaitAsync(shutdownToken));
        fixture._blockingPipelineRelease = release;
        return fixture;
    }

    /// <summary>
    ///     Asynchronously creates an initialized engine host whose pipeline observes user cancellation in flight.
    /// </summary>
    /// <returns>A task whose result contains the initialized hosting fixture.</returns>
    internal static async Task<EngineHostTestFixture> CreateCancellationAwarePipelineAsync()
    {
        var started = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var fixture = await CreateWithTemporaryDirectoryAsync(
            async (_, userCancellationToken, _) =>
            {
                started.TrySetResult();
                await Task.Delay(Timeout.InfiniteTimeSpan, userCancellationToken);
            });
        fixture._pipelineStarted = started;
        return fixture;
    }

    /// <summary>
    ///     Asynchronously reopens the supplied fixture's local state through a new engine host.
    /// </summary>
    /// <param name="seedFixture">The fixture that seeded durable local state. Cannot be <see langword="null" />.</param>
    /// <returns>A task whose result contains the re-opened hosting fixture.</returns>
    internal static async Task<EngineHostTestFixture> ReopenAsync(EngineHostTestFixture seedFixture, Guid? expectedRecoveryRunId = null)
    {
        ArgumentNullException.ThrowIfNull(seedFixture);
        var localStateDirectory = seedFixture._temporaryDirectory?.DirectoryPath
            ?? throw new InvalidOperationException("Only an owning fixture can supply local state for reopening.");
        await seedFixture.DisposeHostAsync();
        return await CreateAsync(localStateDirectory, null, null, false, expectedRecoveryRunId);
    }

    /// <summary>
    ///     Asynchronously accepts an analysis run and wakes the processor for the default repository.
    /// </summary>
    /// <returns>A task whose result contains the accepted run identifier.</returns>
    internal Task<Guid> AcceptRunAsync() =>
        this.AcceptRunAsync("repository");

    /// <summary>
    ///     Asynchronously accepts an analysis run and wakes the processor for another repository.
    /// </summary>
    /// <returns>A task whose result contains the accepted run identifier.</returns>
    internal Task<Guid> AcceptRunForAnotherRepositoryAsync() =>
        this.AcceptRunAsync("another-repository");

    /// <summary>
    ///     Asynchronously accepts an analysis run without waking the processor.
    /// </summary>
    /// <returns>A task whose result contains the accepted run identifier.</returns>
    internal async Task<Guid> AcceptRunWithoutSignalingAsync()
    {
        const string repositoryName = "repository";
        await this.EnsureRepositoryIsRecordedAsync(repositoryName);
        await using var scope = this.Host.Services.CreateAsyncScope();
        var services = scope.ServiceProvider;
        var pathKeyProvider = services.GetRequiredService<ICanonicalRepositoryPathKeyProvider>();
        var store = services.GetRequiredService<IAnalysisRunStore>();
        var canonicalPath = this.RepositoryPathFor(repositoryName);
        var requestedAt = TimeProvider.System.GetUtcNow().ToUnixTimeMilliseconds();
        var acceptance = new AnalysisRunAcceptance(
            pathKeyProvider.CreateKey(canonicalPath),
            repositoryName,
            canonicalPath,
            "0123456789abcdef0123456789abcdef01234567",
            "refs/heads/main",
            "89abcdef0123456789abcdef0123456789abcdef",
            new string('0', 64),
            null,
            requestedAt);
        var result = await store.CreateOrReturnActiveAsync(acceptance, TestContext.Current.CancellationToken);
        Assert.True(result.IsSuccess);
        Assert.Equal(AnalysisStartOutcomeKind.Accepted, result.Data!.Kind);
        return result.Data.RunId!.Value;
    }

    /// <summary>
    ///     Asynchronously reads one durable analysis-run projection.
    /// </summary>
    /// <param name="runId">The identifier of the run to read.</param>
    /// <returns>A task whose result contains the current run detail.</returns>
    internal async Task<AnalysisRunDetail> PollOnceAsync(Guid runId)
    {
        await using var scope = this.Host.Services.CreateAsyncScope();
        var store = scope.ServiceProvider.GetRequiredService<IAnalysisRunStore>();
        var result = await store.GetDetailAsync(runId, TestContext.Current.CancellationToken);
        Assert.True(result.IsSuccess);
        return result.Data!;
    }

    /// <summary>
    ///     Asynchronously polls until the selected run reaches a terminal state.
    /// </summary>
    /// <param name="runId">The identifier of the run to poll.</param>
    /// <param name="timeout">The maximum polling duration.</param>
    /// <returns>A task whose result contains the terminal run detail.</returns>
    internal Task<AnalysisRunDetail> PollUntilTerminalAsync(Guid runId, TimeSpan timeout) =>
        this.PollUntilAsync(runId, candidate => candidate.Terminal is not null, timeout);

    /// <summary>
    ///     Asynchronously polls until the selected run matches the supplied condition.
    /// </summary>
    /// <param name="runId">The identifier of the run to poll.</param>
    /// <param name="condition">The durable condition that ends polling. Cannot be <see langword="null" />.</param>
    /// <param name="timeout">The maximum polling duration.</param>
    /// <returns>A task whose result contains the matching run detail.</returns>
    internal async Task<AnalysisRunDetail> PollUntilAsync(
        Guid runId,
        Func<AnalysisRunDetail, bool> condition,
        TimeSpan timeout)
    {
        ArgumentNullException.ThrowIfNull(condition);
        var deadline = DateTimeOffset.UtcNow + timeout;
        while (DateTimeOffset.UtcNow <= deadline)
        {
            var projection = await this.PollOnceAsync(runId);
            if (condition(projection))
            {
                return projection;
            }

            await Task.Delay(TimeSpan.FromMilliseconds(25), TestContext.Current.CancellationToken);
        }

        throw new TimeoutException($"Analysis run {runId} did not reach the expected state within {timeout}.");
    }

    /// <summary>
    ///     Asynchronously creates and takes a durable run without starting the processor host.
    /// </summary>
    /// <returns>A task whose result contains the active orphaned run identifier.</returns>
    internal async Task<Guid> SeedActiveRunLeftByAPreviousProcessAsync()
    {
        var runId = await this.AcceptRunAsync();
        await using var scope = this.Host.Services.CreateAsyncScope();
        var store = scope.ServiceProvider.GetRequiredService<IAnalysisRunStore>();
        var take = await store.TakeNextPendingAsync(TestContext.Current.CancellationToken);
        Assert.True(take.IsSuccess);
        Assert.Equal(runId, take.Data!.Value);
        return runId;
    }

    /// <summary>
    ///     Asynchronously waits until the processor takes the selected run.
    /// </summary>
    /// <param name="runId">The identifier of the run to observe.</param>
    /// <param name="timeout">The maximum polling duration.</param>
    /// <returns>A task that represents the asynchronous operation.</returns>
    internal async Task WaitUntilTakenAsync(Guid runId, TimeSpan timeout)
    {
        await this.PollUntilAsync(runId, candidate => candidate.State == AnalysisRunState.Capturing, timeout);
    }

    /// <summary>
    ///     Asynchronously waits until the controlled pipeline begins running a taken analysis run.
    /// </summary>
    /// <param name="cancellationToken">The token that bounds the wait.</param>
    /// <returns>A task that represents the asynchronous operation.</returns>
    internal Task WaitUntilPipelineStartsAsync(CancellationToken cancellationToken) =>
        (this._pipelineStarted ?? throw new InvalidOperationException("The fixture has no pipeline-start signal."))
        .Task.WaitAsync(cancellationToken);

    /// <summary>
    ///     Asynchronously waits until the protocol host begins reading input.
    /// </summary>
    /// <param name="cancellationToken">The token that bounds the wait.</param>
    /// <returns>A task that represents the asynchronous operation.</returns>
    internal Task WaitForProtocolReadAsync(CancellationToken cancellationToken) =>
        this._protocolTransport.WaitForReadAsync(cancellationToken);

    /// <summary>
    ///     Asynchronously requests cancellation through the production analysis coordinator.
    /// </summary>
    /// <param name="runId">The identifier of the run to cancel.</param>
    /// <returns>A task that represents the asynchronous operation.</returns>
    internal async Task RequestCancellationAsync(Guid runId)
    {
        await using var scope = this.Host.Services.CreateAsyncScope();
        var coordinator = scope.ServiceProvider.GetRequiredService<IAnalysisRunCoordinator>();
        var result = await coordinator.CancelAsync(runId, TestContext.Current.CancellationToken);
        Assert.True(result.IsSuccess);
    }

    /// <summary>
    ///     Asynchronously routes one request through the production protocol host and a real request scope.
    /// </summary>
    /// <param name="action">The approved protocol action. Cannot be <see langword="null" />.</param>
    /// <param name="parameters">The action parameters, or an undefined value when the action has no parameters.</param>
    /// <returns>A task whose result contains the correlated protocol response.</returns>
    internal async Task<ProtocolResponse> SendRealProtocolRequestAsync(string action, JsonElement parameters)
    {
        await using var scope = this.Host.Services.CreateAsyncScope();
        var protocolHost = this.Host.Services.GetServices<IHostedService>().OfType<EngineProtocolHost>().Single();
        var request = new EngineProtocolRequest
        {
            ProtocolVersion = EngineProtocolConstants.CurrentVersion,
            RequestId = $"repository-lock-{action}",
            Action = action,
            Parameters = parameters,
        };
        return await protocolHost.ProcessAsync(request, scope.ServiceProvider, TestContext.Current.CancellationToken);
    }

    /// <summary>
    ///     Creates minimal parameters for an action against the fixture's retained repository.
    /// </summary>
    /// <param name="action">The approved protocol action. Cannot be <see langword="null" />.</param>
    /// <param name="runId">The active run identifier used by run-keyed actions.</param>
    /// <returns>The serialized action parameters, or an undefined value when the action has no parameters.</returns>
    internal JsonElement RequestParametersFor(string action, Guid? runId = null) => action switch
    {
        "repositories.open" => JsonSerializer.SerializeToElement(new { path = this._repositoryPath }),
        "repositories.removeRecent" => JsonSerializer.SerializeToElement(new { repositoryId = this._repositoryId.ToString("D") }),
        "comparisons.listTargets" => JsonSerializer.SerializeToElement(new { path = this._repositoryPath }),
        "comparisons.prepare" => JsonSerializer.SerializeToElement(new { path = this._repositoryPath, target = "refs/heads/main" }),
        "comparisons.checkFreshness" => JsonSerializer.SerializeToElement(
            new { path = this._repositoryPath, target = "refs/heads/main", freshnessToken = new string('0', 64) }),
        "comparisons.checkRemoteBaseline" or "comparisons.refreshRemoteBaseline" => JsonSerializer.SerializeToElement(
            new { path = this._repositoryPath, target = "refs/remotes/origin/main" }),
        "analysis.getActive" => JsonSerializer.SerializeToElement(new { path = this._repositoryPath }),
        "analysis.pollRun" or "analysis.cancel" => JsonSerializer.SerializeToElement(
            new { runId = (runId ?? throw new ArgumentNullException(nameof(runId))).ToString("D") }),
        "repositories.listRecent" or "preferences.getColorTheme" or "engine.checkStatus" => default,
        "preferences.setColorTheme" => JsonSerializer.SerializeToElement(new { colorTheme = "dark" }),
        _ => throw new ArgumentOutOfRangeException(nameof(action), action, "The action has no repository-lock fixture parameters."),
    };

    /// <summary>
    ///     Asserts that a response contains the repository-reservation error.
    /// </summary>
    /// <param name="response">The protocol response to inspect. Cannot be <see langword="null" />.</param>
    internal void AssertBusyError(ProtocolResponse response)
    {
        var errorResponse = Assert.IsType<ProtocolErrorResponse>(response);
        Assert.Contains(errorResponse.Errors, error => error.Code == "repository.busy");
    }

    /// <summary>
    ///     Asserts that a response does not contain the repository-reservation error.
    /// </summary>
    /// <param name="response">The protocol response to inspect. Cannot be <see langword="null" />.</param>
    internal void AssertNotBusyError(ProtocolResponse response)
    {
        if (response is ProtocolErrorResponse errorResponse)
        {
            Assert.DoesNotContain(errorResponse.Errors, error => error.Code == "repository.busy");
        }
    }

    /// <summary>
    ///     Lists every action covered by the observable repository-lock matrix.
    /// </summary>
    /// <returns>The complete ordered action set.</returns>
    internal IReadOnlyList<string> EveryRepositoryScopedAction() =>
    [
        "repositories.open",
        "repositories.removeRecent",
        "comparisons.listTargets",
        "comparisons.prepare",
        "comparisons.checkFreshness",
        "comparisons.checkRemoteBaseline",
        "comparisons.refreshRemoteBaseline",
        "repositories.listRecent",
        "preferences.getColorTheme",
        "preferences.setColorTheme",
        "engine.checkStatus",
        "analysis.getActive",
        "analysis.pollRun",
        "analysis.cancel",
    ];

    /// <summary>
    ///     Releases the controlled blocking pipeline.
    /// </summary>
    internal void ReleaseBlockingPipeline() => this._blockingPipelineRelease?.TrySetResult();

    /// <summary>
    ///     Asynchronously stops the hosted engine services.
    /// </summary>
    /// <param name="cancellationToken">The token that bounds host shutdown.</param>
    /// <returns>A task that represents the asynchronous operation.</returns>
    internal Task StopAsync(CancellationToken cancellationToken) => this.Host.StopAsync(cancellationToken);

    /// <inheritdoc />
    public async ValueTask DisposeAsync()
    {
        await this.DisposeHostAsync();
        this._temporaryDirectory?.Dispose();
    }

    private static async Task<EngineHostTestFixture> CreateAsync(
        string? localStateDirectory = null,
        TemporaryDirectory? temporaryDirectory = null,
        Func<Guid, CancellationToken, CancellationToken, Task>? pipelineRun = null,
        bool completeSuccessfulRuns = false,
        Guid? expectedRecoveryRunId = null)
    {
        var resolvedDirectory = localStateDirectory ?? temporaryDirectory?.DirectoryPath
            ?? throw new ArgumentException("A local-state directory is required.", nameof(localStateDirectory));
        var builder = Microsoft.Extensions.Hosting.Host.CreateApplicationBuilder(
            new HostApplicationBuilderSettings
            {
                ContentRootPath = AppContext.BaseDirectory,
            });
        builder.Configuration[LocalStateConstants.DirectoryConfigurationKey] = resolvedDirectory;
        builder.Configuration[EngineLoggingConstants.FileDirectoryConfigurationKey] = Path.Combine(resolvedDirectory, "logs");

        builder.ConfigureContainer(
            new DefaultServiceProviderFactory(new ServiceProviderOptions { ValidateOnBuild = true, ValidateScopes = true }),
            static _ => { });
        builder.AddEngineLogging();
        builder.AddRuntimeServices();
        builder.AddLocalStateServices();
        builder.AddPreferenceServices();
        builder.AddEngineStatusServices();
        builder.AddRepositoryServices();
        builder.AddComparisonServices();
        builder.AddAnalysisRunServices();
        builder.AddProtocolServices();
        builder.AddActionHandlers();
        IHost? host = null;
        var protocolTransport = new BlockingProtocolTransport(
            expectedRecoveryRunId is null
                ? null
                : async cancellationToken =>
                {
                    await using var scope = host!.Services.CreateAsyncScope();
                    var store = scope.ServiceProvider.GetRequiredService<IAnalysisRunStore>();
                    var projection = await store.GetDetailAsync(expectedRecoveryRunId.Value, cancellationToken);
                    if (projection is not { IsSuccess: true, Data.State: AnalysisRunState.Interrupted })
                    {
                        throw new InvalidOperationException("Protocol input was observed before startup recovery completed.");
                    }
                });
        builder.Services.Replace(ServiceDescriptor.Singleton<IEngineProtocolTransport>(protocolTransport));
        builder.Services.Replace(ServiceDescriptor.Scoped<IGitComparisonFreshnessChecker, FixtureGitComparisonFreshnessChecker>());

        if (pipelineRun is not null)
        {
            builder.Services.Replace(
                ServiceDescriptor.Scoped<IAnalysisPipeline>(
                    serviceProvider => new AnalysisProcessorTestPipeline(
                        pipelineRun,
                        completeSuccessfulRuns,
                        serviceProvider.GetRequiredService<IAnalysisRunStore>(),
                        serviceProvider.GetRequiredService<TimeProvider>())));
        }

        EngineStartupValidator.Validate(builder.Services);
        host = builder.Build();
        var repositoryPath = Path.Combine(resolvedDirectory, "repository");
        Directory.CreateDirectory(repositoryPath);
        var repositoryPathResult = await new PhysicalRepositoryPathResolver().ResolveAsync(
            repositoryPath,
            TestContext.Current.CancellationToken);
        Assert.True(repositoryPathResult.IsSuccess);
        repositoryPath = repositoryPathResult.Data!;
        var fixture = new EngineHostTestFixture(host, temporaryDirectory, protocolTransport, repositoryPath);
        await fixture.InitializeLocalStateAsync();
        fixture._repositoryId = await fixture.EnsureRepositoryIsRecordedAsync("repository");
        return fixture;
    }

    private static Task<EngineHostTestFixture> CreateWithTemporaryDirectoryAsync(
        Func<Guid, CancellationToken, CancellationToken, Task>? pipelineRun,
        bool completeSuccessfulRuns = false)
    {
        var temporaryDirectory = new TemporaryDirectory();
        return CreateAsync(temporaryDirectory.DirectoryPath, temporaryDirectory, pipelineRun, completeSuccessfulRuns);
    }

    private async Task<Guid> AcceptRunAsync(string repositoryName)
    {
        await this.EnsureRepositoryIsRecordedAsync(repositoryName);
        await using var scope = this.Host.Services.CreateAsyncScope();
        var services = scope.ServiceProvider;
        var coordinator = services.GetRequiredService<IAnalysisRunCoordinator>();
        var canonicalPath = this.RepositoryPathFor(repositoryName);
        var acceptanceResult = await coordinator.StartAsync(
            canonicalPath,
            "refs/heads/main",
            new string('0', 64),
            null,
            TestContext.Current.CancellationToken);
        Assert.True(acceptanceResult.IsSuccess);
        Assert.Equal(AnalysisStartOutcomeKind.Accepted, acceptanceResult.Data!.Kind);

        return acceptanceResult.Data.RunId!.Value;
    }

    private async Task<Guid> EnsureRepositoryIsRecordedAsync(string repositoryName)
    {
        await using var scope = this.Host.Services.CreateAsyncScope();
        var services = scope.ServiceProvider;
        var historyStore = services.GetRequiredService<IRepositoryHistoryStore>();
        var pathKeyProvider = services.GetRequiredService<ICanonicalRepositoryPathKeyProvider>();
        var canonicalPath = this.RepositoryPathFor(repositoryName);
        Directory.CreateDirectory(canonicalPath);
        var historyResult = await historyStore.RecordOpenAsync(
            canonicalPath,
            pathKeyProvider.CreateKey(canonicalPath),
            repositoryName,
            TimeProvider.System.GetUtcNow().ToUnixTimeMilliseconds(),
            TestContext.Current.CancellationToken);
        Assert.True(historyResult.IsSuccess);
        return historyResult.Data!.RepositoryId;
    }

    private string RepositoryPathFor(string repositoryName) =>
        Path.Combine(Path.GetDirectoryName(this._repositoryPath)!, repositoryName);

    private async Task InitializeLocalStateAsync()
    {
        await using var scope = this.Host.Services.CreateAsyncScope();
        var initializer = scope.ServiceProvider.GetRequiredService<ILocalStateInitializer>();
        var result = await initializer.InitializeAsync(TestContext.Current.CancellationToken);
        Assert.True(result.IsSuccess);
    }

    private async Task DisposeHostAsync()
    {
        if (this._host is not null)
        {
            await this._host.StopAsync(CancellationToken.None);
            this._host.Dispose();
            this._host = null;
        }
    }
}
