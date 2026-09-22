using System.Text.Json;
using ChangeLens.Core.LocalState.Interfaces;
using ChangeLens.Engine.Hosting.Extensions;
using ChangeLens.Engine.Hosting.Helpers;
using ChangeLens.Engine.IntegrationTests.Hosting.Support;
using ChangeLens.Engine.IntegrationTests.Support;
using ChangeLens.Engine.Logging.Constants;
using ChangeLens.Engine.Logging.Extensions;
using ChangeLens.Engine.Protocol.Constants;
using ChangeLens.Engine.Protocol.Interfaces;
using ChangeLens.Engine.Protocol.Models;
using ChangeLens.Engine.Protocol.Services;
using ChangeLens.Infrastructure.LocalState.Constants;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using Xunit;
using Xunit.Sdk;

namespace ChangeLens.Engine.IntegrationTests.Analysis.Support;

/// <summary>
///     Runs the real production analysis pipeline in-process over a real Git repository and controlled local state.
/// </summary>
internal sealed class AnalysisPipelineTestHost : IAsyncDisposable
{
    private readonly TemporaryDirectory _temporaryDirectory;
    private IHost? _host;

    private AnalysisPipelineTestHost(IHost host, TemporaryDirectory temporaryDirectory)
    {
        this._host = host;
        this._temporaryDirectory = temporaryDirectory;
    }

    /// <summary>
    ///     Gets the initialized production-composed engine host.
    /// </summary>
    internal IHost Host => this._host ?? throw new ObjectDisposedException(nameof(AnalysisPipelineTestHost));

    /// <summary>
    ///     Asynchronously creates an initialized host with production capture, freshness checking, and pipeline.
    /// </summary>
    /// <param name="configureServices">
    ///     The optional caller service substitution applied after production composition, or <see langword="null" />.
    /// </param>
    /// <returns>A task whose result contains the initialized host.</returns>
    internal static async Task<AnalysisPipelineTestHost> CreateAsync(Action<IServiceCollection>? configureServices = null)
    {
        var temporaryDirectory = new TemporaryDirectory();
        var localStateDirectory = Path.Combine(temporaryDirectory.DirectoryPath, "local-state");
        Directory.CreateDirectory(localStateDirectory);
        var builder = Microsoft.Extensions.Hosting.Host.CreateApplicationBuilder(
            new HostApplicationBuilderSettings
            {
                ContentRootPath = AppContext.BaseDirectory,
            });
        builder.Configuration[LocalStateConstants.DirectoryConfigurationKey] = localStateDirectory;
        builder.Configuration[EngineLoggingConstants.FileDirectoryConfigurationKey] = Path.Combine(localStateDirectory, "logs");

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

        var protocolTransport = new BlockingProtocolTransport();
        builder.Services.Replace(ServiceDescriptor.Singleton<IEngineProtocolTransport>(protocolTransport));
        configureServices?.Invoke(builder.Services);

        EngineStartupValidator.Validate(builder.Services);
        var host = builder.Build();
        var testHost = new AnalysisPipelineTestHost(host, temporaryDirectory);
        await testHost.InitializeLocalStateAsync();
        return testHost;
    }

    /// <summary>
    ///     Asynchronously starts the hosted engine services.
    /// </summary>
    /// <param name="cancellationToken">The token that bounds host startup.</param>
    /// <returns>A task that represents the asynchronous operation.</returns>
    internal Task StartAsync(CancellationToken cancellationToken) => this.Host.StartAsync(cancellationToken);

    /// <summary>
    ///     Asynchronously stops the hosted engine services.
    /// </summary>
    /// <param name="cancellationToken">The token that bounds host shutdown.</param>
    /// <returns>A task that represents the asynchronous operation.</returns>
    internal Task StopAsync(CancellationToken cancellationToken) => this.Host.StopAsync(cancellationToken);

    /// <summary>
    ///     Asynchronously records one repository through the production open action.
    /// </summary>
    /// <param name="path">The repository path.</param>
    /// <returns>A task that represents the asynchronous operation.</returns>
    internal async Task OpenRepositoryAsync(string path)
    {
        using var response = await this.SendAsync("repositories.open", JsonSerializer.SerializeToElement(new { path }));
        AssertResult(response);
    }

    /// <summary>
    ///     Asynchronously prepares a comparison and returns its freshness token.
    /// </summary>
    /// <param name="path">The repository path.</param>
    /// <param name="target">The comparison target.</param>
    /// <returns>The current freshness token.</returns>
    internal async Task<string> PrepareFreshnessTokenAsync(string path, string target)
    {
        using var response = await this.SendAsync("comparisons.prepare", JsonSerializer.SerializeToElement(new { path, target }));
        AssertResult(response);
        return response.RootElement.GetProperty("result").GetProperty("freshnessToken").GetString()
            ?? throw new XunitException("Comparison preparation returned no freshness token.");
    }

    /// <summary>
    ///     Asynchronously starts an analysis run and returns its identifier.
    /// </summary>
    /// <param name="path">The repository path.</param>
    /// <param name="target">The comparison target.</param>
    /// <param name="freshnessToken">The prepared freshness token.</param>
    /// <returns>The accepted run identifier.</returns>
    internal async Task<string> StartAsync(string path, string target, string freshnessToken)
    {
        using var response = await this.SendAsync(
            "analysis.start",
            JsonSerializer.SerializeToElement(new { path, target, freshnessToken }));
        AssertResult(response);
        var result = response.RootElement.GetProperty("result");
        Assert.Equal("accepted", result.GetProperty("state").GetString());
        return result.GetProperty("runId").GetString() ?? throw new XunitException("Analysis start returned no run identifier.");
    }

    /// <summary>
    ///     Asynchronously polls one run and returns its raw poll response JSON.
    /// </summary>
    /// <param name="runId">The analysis run identifier.</param>
    /// <returns>A task whose result contains the poll response the caller must dispose.</returns>
    internal Task<JsonDocument> PollAsync(string runId) =>
        this.SendAsync("analysis.pollRun", JsonSerializer.SerializeToElement(new { runId }));

    /// <summary>
    ///     Asynchronously polls one run until its terminal summary is available.
    /// </summary>
    /// <param name="runId">The analysis run identifier.</param>
    /// <param name="timeout">The maximum observation interval.</param>
    /// <returns>A task whose result contains the terminal poll response the caller must dispose.</returns>
    internal async Task<JsonDocument> PollUntilTerminalAsync(string runId, TimeSpan timeout)
    {
        var deadline = DateTimeOffset.UtcNow + timeout;
        while (DateTimeOffset.UtcNow <= deadline)
        {
            var response = await this.PollAsync(runId);
            AssertResult(response);
            if (response.RootElement.GetProperty("result").GetProperty("terminal").ValueKind != JsonValueKind.Null)
            {
                return response;
            }

            response.Dispose();
            await Task.Delay(TimeSpan.FromMilliseconds(25), TestContext.Current.CancellationToken);
        }

        throw new XunitException($"Analysis run {runId} did not reach terminal state within {timeout}.");
    }

    /// <summary>
    ///     Asynchronously requests cancellation of one run.
    /// </summary>
    /// <param name="runId">The analysis run identifier.</param>
    /// <returns>A task whose result contains the cancellation response the caller must dispose.</returns>
    internal Task<JsonDocument> CancelAsync(string runId) =>
        this.SendAsync("analysis.cancel", JsonSerializer.SerializeToElement(new { runId }));

    /// <inheritdoc />
    public async ValueTask DisposeAsync()
    {
        await this.DisposeHostAsync();
        this._temporaryDirectory.Dispose();
    }

    private static void AssertResult(JsonDocument response) =>
        Assert.Equal("result", response.RootElement.GetProperty("type").GetString());

    private async Task<JsonDocument> SendAsync(string action, JsonElement parameters)
    {
        await using var scope = this.Host.Services.CreateAsyncScope();
        var protocolHost = this.Host.Services.GetServices<IHostedService>().OfType<EngineProtocolHost>().Single();
        var request = new EngineProtocolRequest
        {
            ProtocolVersion = EngineProtocolConstants.CurrentVersion,
            RequestId = $"analysis-pipeline-{action}-{Guid.NewGuid():N}",
            Action = action,
            Parameters = parameters,
        };
        var response = await protocolHost.ProcessAsync(request, scope.ServiceProvider, TestContext.Current.CancellationToken);
        var serialized = this.Host.Services.GetRequiredService<IEngineProtocolSerializer>().SerializeResponse(response);
        if (serialized.IsFailure)
        {
            throw new XunitException("The protocol response could not be serialized with the production policy.");
        }

        return JsonDocument.Parse(serialized.Data!);
    }

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