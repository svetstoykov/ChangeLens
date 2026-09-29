using System.Text.Json;
using ChangeLens.Core.ModelCompletion.Interfaces;
using ChangeLens.Engine.IntegrationTests.Protocol.Support;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Xunit;

namespace ChangeLens.Engine.IntegrationTests.Analysis.Support;

/// <summary>
///     Runs one scripted analysis to its terminal poll over a controlled repository.
/// </summary>
internal static class ScriptedPipelineRun
{
    /// <summary>
    ///     Asynchronously analyzes the repository's default comparison with the scripted client and polls until the run is terminal.
    /// </summary>
    /// <param name="repository">The repository to analyze.</param>
    /// <param name="client">The scripted completion client.</param>
    /// <param name="settings">The optional engine configuration overrides keyed by full configuration key.</param>
    /// <param name="timeout">The maximum observation interval.</param>
    /// <returns>A task whose result contains the terminal poll response the caller must dispose.</returns>
    internal static async Task<JsonDocument> RunToTerminalAsync(
        ProtocolTemporaryGitRepository repository,
        ScriptedModelCompletionClient client,
        IReadOnlyDictionary<string, string?>? settings,
        TimeSpan timeout)
    {
        await using var host = await AnalysisPipelineTestHost.CreateAsync(
            services => services.Replace(ServiceDescriptor.Scoped<IModelCompletionClient>(_ => client)), settings);
        await host.StartAsync(TestContext.Current.CancellationToken);
        await host.OpenRepositoryAsync(repository.Path);
        var freshnessToken = await host.PrepareFreshnessTokenAsync(repository.Path, repository.DefaultTarget);
        var runId = await host.StartAsync(repository.Path, repository.DefaultTarget, freshnessToken);
        var terminal = await host.PollUntilTerminalAsync(runId, timeout);
        await host.StopAsync(TestContext.Current.CancellationToken);
        return terminal;
    }
}
