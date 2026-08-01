using System.Diagnostics;
using System.Text.Json;
using ChangeLens.Engine.AnalysisRuns.Constants;
using ChangeLens.Engine.IntegrationTests.Support;
using Xunit;
using Xunit.Sdk;

namespace ChangeLens.Engine.IntegrationTests.Protocol.Support;

/// <summary>
///     Owns a real Engine child process for analysis protocol tests.
/// </summary>
internal sealed class ProtocolTestEngine : IAsyncDisposable
{
    private readonly Process _process;
    private readonly TemporaryDirectory _localStateDirectory;
    private readonly string? _pipelineReleaseFile;
    private readonly string? _pipelineEnteredFile;
    private readonly string? _pipelineStepsStartedFile;

    private ProtocolTestEngine(
        Process process,
        TemporaryDirectory localStateDirectory,
        string? pipelineReleaseFile,
        string? pipelineEnteredFile,
        string? pipelineStepsStartedFile)
    {
        this._process = process;
        this._localStateDirectory = localStateDirectory;
        this._pipelineReleaseFile = pipelineReleaseFile;
        this._pipelineEnteredFile = pipelineEnteredFile;
        this._pipelineStepsStartedFile = pipelineStepsStartedFile;
    }

    /// <summary>Gets the owned Engine process for capacity sampling.</summary>
    public Process Process => this._process;

    /// <summary>Gets a value indicating whether the gated production pipeline began step execution.</summary>
    public bool PipelineStepsStarted => this._pipelineStepsStartedFile is not null && File.Exists(this._pipelineStepsStartedFile);

    /// <summary>Starts a real Engine child process.</summary>
    /// <param name="logDirectory">The directory for Engine logs.</param>
    /// <param name="blockPipelineUntilReleased">Whether analysis pipeline work waits for an explicit release signal.</param>
    /// <returns>A task whose result owns the started Engine process.</returns>
    public static Task<ProtocolTestEngine> StartAsync(string logDirectory, bool blockPipelineUntilReleased = false)
    {
        var engineDll = System.IO.Path.Combine(
            RepositoryPaths.Root,
            "src",
            "engine",
            "ChangeLens.Engine",
            "bin",
            "Debug",
            "net10.0",
            "ChangeLens.Engine.dll");
        if (!File.Exists(engineDll))
        {
            throw new InvalidOperationException("The built Engine DLL must exist before a protocol test starts.");
        }

        var localStateDirectory = new TemporaryDirectory();
        var releaseFile = blockPipelineUntilReleased
            ? System.IO.Path.Combine(localStateDirectory.DirectoryPath, "release-analysis-pipeline")
            : null;
        var enteredFile = blockPipelineUntilReleased
            ? System.IO.Path.Combine(localStateDirectory.DirectoryPath, "entered-analysis-pipeline")
            : null;
        var stepsStartedFile = blockPipelineUntilReleased
            ? System.IO.Path.Combine(localStateDirectory.DirectoryPath, "started-analysis-pipeline-steps")
            : null;
        var startInfo = new ProcessStartInfo("dotnet")
        {
            CreateNoWindow = true,
            RedirectStandardInput = true,
            RedirectStandardOutput = true,
            UseShellExecute = false,
        };
        startInfo.Environment["ChangeLens__LocalState__Directory"] = localStateDirectory.DirectoryPath;
        startInfo.Environment["ChangeLens__Logging__FileDirectory"] = logDirectory;
        startInfo.Environment["Serilog__MinimumLevel__Default"] = "Debug";
        if (releaseFile is not null)
        {
            startInfo.Environment[AnalysisIntegrationTestConstants.PipelineReleaseFileEnvironmentVariable] = releaseFile;
            startInfo.Environment[AnalysisIntegrationTestConstants.PipelineEnteredFileEnvironmentVariable] = enteredFile;
            startInfo.Environment[AnalysisIntegrationTestConstants.PipelineStepsStartedFileEnvironmentVariable] = stepsStartedFile;
        }
        else
        {
            startInfo.Environment.Remove(AnalysisIntegrationTestConstants.PipelineReleaseFileEnvironmentVariable);
            startInfo.Environment.Remove(AnalysisIntegrationTestConstants.PipelineEnteredFileEnvironmentVariable);
            startInfo.Environment.Remove(AnalysisIntegrationTestConstants.PipelineStepsStartedFileEnvironmentVariable);
        }

        startInfo.ArgumentList.Add(engineDll);
        var process = Process.Start(startInfo) ?? throw new InvalidOperationException("The ChangeLens Engine process did not start.");
        return Task.FromResult(new ProtocolTestEngine(process, localStateDirectory, releaseFile, enteredFile, stepsStartedFile));
    }

    /// <summary>Asynchronously sends one protocol request and reads its response.</summary>
    /// <param name="action">The protocol action.</param>
    /// <param name="requestId">The request correlation identifier.</param>
    /// <param name="parametersJson">The serialized parameters object.</param>
    /// <returns>The parsed Engine response.</returns>
    public Task<JsonDocument> SendAsync(string action, string requestId, string parametersJson) =>
        this.SendAsync(action, requestId, parametersJson, TimeSpan.FromSeconds(35));

    /// <summary>Asynchronously waits until the gated pipeline has been entered.</summary>
    /// <param name="timeout">The maximum wait interval.</param>
    /// <returns>A task that represents the asynchronous operation.</returns>
    public async Task WaitUntilPipelineBlockedAsync(TimeSpan timeout)
    {
        if (this._pipelineEnteredFile is null)
        {
            throw new InvalidOperationException("This Engine was not started with a blocked analysis pipeline.");
        }

        var startedAt = Stopwatch.GetTimestamp();
        while (!File.Exists(this._pipelineEnteredFile))
        {
            var remaining = timeout - Stopwatch.GetElapsedTime(startedAt);
            if (remaining <= TimeSpan.Zero)
            {
                throw new XunitException($"The analysis pipeline was not observed at its gate within {timeout}.");
            }

            await Task.Delay(
                remaining < TimeSpan.FromMilliseconds(10) ? remaining : TimeSpan.FromMilliseconds(10),
                TestContext.Current.CancellationToken);
        }
    }

    private async Task<JsonDocument> SendAsync(
        string action,
        string requestId,
        string parametersJson,
        TimeSpan responseTimeout)
    {
        using var parameters = JsonDocument.Parse(parametersJson);
        var request = JsonSerializer.Serialize(
            new
            {
                protocolVersion = 1,
                requestId,
                action,
                parameters = parameters.RootElement,
            });
        await this._process.StandardInput.WriteLineAsync(request);
        var response = await this._process.StandardOutput.ReadLineAsync(TestContext.Current.CancellationToken)
            .AsTask()
            .WaitAsync(responseTimeout, TestContext.Current.CancellationToken);
        if (string.IsNullOrWhiteSpace(response))
        {
            throw new XunitException("The Engine returned no protocol response.");
        }

        return JsonDocument.Parse(response);
    }

    /// <summary>Asynchronously records a repository through the real Engine before analysis acceptance.</summary>
    /// <param name="path">The repository path.</param>
    /// <returns>A task that represents the protocol exchange.</returns>
    public async Task OpenRepositoryAsync(string path)
    {
        var requestId = "analysis-fixture-open-" + Guid.NewGuid().ToString("N");
        using var response = await this.SendAsync(
            "repositories.open",
            requestId,
            JsonSerializer.Serialize(new { path }));
        ProtocolResponseAssertions.AssertResultEnvelope(response, requestId);
    }

    /// <summary>Asynchronously prepares a comparison through the real Engine and returns its freshness token.</summary>
    /// <param name="path">The repository path.</param>
    /// <param name="target">The comparison target.</param>
    /// <returns>The current freshness token.</returns>
    public async Task<string> PrepareFreshnessTokenAsync(string path, string target)
    {
        var requestId = "analysis-fixture-prepare-" + Guid.NewGuid().ToString("N");
        using var response = await this.SendAsync(
            "comparisons.prepare",
            requestId,
            JsonSerializer.Serialize(new { path, target }));
        var result = ProtocolResponseAssertions.AssertResultEnvelope(response, requestId);

        return result.GetProperty("freshnessToken").GetString()
            ?? throw new XunitException("Comparison preparation returned no freshness token.");
    }

    /// <summary>Asynchronously polls one run until its terminal projection is available.</summary>
    /// <param name="runId">The analysis run identifier.</param>
    /// <param name="timeout">The maximum observation interval.</param>
    /// <returns>The terminal poll response.</returns>
    public async Task<JsonDocument> PollUntilTerminalAsync(string runId, TimeSpan timeout)
    {
        var startedAt = Stopwatch.GetTimestamp();
        string? lastState = null;
        while (Stopwatch.GetElapsedTime(startedAt) < timeout)
        {
            var remaining = timeout - Stopwatch.GetElapsedTime(startedAt);
            if (remaining <= TimeSpan.Zero)
            {
                break;
            }

            var requestId = "analysis-poll-" + Guid.NewGuid().ToString("N");
            JsonDocument response;
            try
            {
                response = await this.SendAsync(
                    "analysis.pollRun",
                    requestId,
                    JsonSerializer.Serialize(new { runId }),
                    remaining);
            }
            catch (TimeoutException)
            {
                break;
            }

            var result = ProtocolResponseAssertions.AssertResultEnvelope(response, requestId);
            ProtocolResponseAssertions.AssertAnalysisRunProjection(result);
            lastState = result.GetProperty("state").GetString();
            if (result.GetProperty("terminal").ValueKind != JsonValueKind.Null)
            {
                return response;
            }

            response.Dispose();
            remaining = timeout - Stopwatch.GetElapsedTime(startedAt);
            if (remaining > TimeSpan.Zero)
            {
                await Task.Delay(
                    remaining < TimeSpan.FromMilliseconds(25) ? remaining : TimeSpan.FromMilliseconds(25),
                    TestContext.Current.CancellationToken);
            }
        }

        throw new XunitException($"Analysis run {runId} did not reach terminal state within {timeout}; last state was {lastState ?? "unobserved"}.");
    }

    /// <summary>Releases analysis pipeline work blocked by this test process.</summary>
    public void ReleaseBlockedPipeline()
    {
        if (this._pipelineReleaseFile is null)
        {
            throw new InvalidOperationException("This Engine was not started with a blocked analysis pipeline.");
        }

        File.WriteAllText(this._pipelineReleaseFile, "released");
    }

    /// <inheritdoc />
    public async ValueTask DisposeAsync()
    {
        if (this._pipelineReleaseFile is not null && !File.Exists(this._pipelineReleaseFile))
        {
            File.WriteAllText(this._pipelineReleaseFile, "released");
        }

        try
        {
            this._process.StandardInput.Close();
            await this._process.WaitForExitAsync(CancellationToken.None).WaitAsync(TimeSpan.FromSeconds(10), CancellationToken.None);
        }
        catch (Exception exception) when (exception is TimeoutException or OperationCanceledException)
        {
            if (!this._process.HasExited)
            {
                this._process.Kill(entireProcessTree: true);
            }

            await this._process.WaitForExitAsync(CancellationToken.None).WaitAsync(TimeSpan.FromSeconds(10), CancellationToken.None);
        }
        finally
        {
            this._process.Dispose();
            this._localStateDirectory.Dispose();
        }
    }

}
