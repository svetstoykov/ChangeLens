#if DEBUG
using ChangeLens.Engine.AnalysisRuns.Constants;
using ChangeLens.Engine.AnalysisRuns.Interfaces;

namespace ChangeLens.Engine.AnalysisRuns.Services;

/// <summary>
///     Gates Debug integration-test pipeline work on a process-visible release file.
/// </summary>
/// <remarks>
///     The Engine registers this scoped decorator only when a Debug child process receives the integration-test
///     release-file setting. Production hosts resolve <see cref="ShallowAnalysisPipeline" /> directly.
/// </remarks>
/// <param name="pipeline">The production shallow pipeline to run after release. Cannot be <see langword="null" />.</param>
/// <param name="releaseFile">The full path to the release file. Cannot be <see langword="null" />.</param>
/// <param name="enteredFile">The full path that records gate entry. Cannot be <see langword="null" />.</param>
/// <param name="stepsStartedFile">The full path that records production step execution. Cannot be <see langword="null" />.</param>
/// <exception cref="ArgumentNullException"><paramref name="pipeline" /> is <see langword="null" />.</exception>
/// <exception cref="ArgumentException">
///     <paramref name="releaseFile" />, <paramref name="enteredFile" />, or <paramref name="stepsStartedFile" />
///     is empty or contains only whitespace.
/// </exception>
internal sealed class FileGatedAnalysisPipeline(
    IAnalysisPipeline pipeline,
    string releaseFile,
    string enteredFile,
    string stepsStartedFile) : IAnalysisPipeline
{
    private readonly IAnalysisPipeline _pipeline = pipeline ?? throw new ArgumentNullException(nameof(pipeline));
    private readonly string _releaseFile = !string.IsNullOrWhiteSpace(releaseFile)
        ? releaseFile
        : throw new ArgumentException("The pipeline release file must not be blank.", nameof(releaseFile));
    private readonly string _enteredFile = !string.IsNullOrWhiteSpace(enteredFile)
        ? enteredFile
        : throw new ArgumentException("The pipeline entered file must not be blank.", nameof(enteredFile));
    private readonly string _stepsStartedFile = !string.IsNullOrWhiteSpace(stepsStartedFile)
        ? stepsStartedFile
        : throw new ArgumentException("The pipeline steps-started file must not be blank.", nameof(stepsStartedFile));

    /// <inheritdoc />
    public async Task RunAsync(
        Guid runId,
        CancellationToken userCancellationToken,
        CancellationToken shutdownToken)
    {
        File.WriteAllText(this._enteredFile, "entered");
        using var gateCancellation = CancellationTokenSource.CreateLinkedTokenSource(userCancellationToken, shutdownToken);
        while (!File.Exists(this._releaseFile))
        {
            await Task.Delay(AnalysisIntegrationTestConstants.PipelineReleasePollInterval, gateCancellation.Token);
        }

        gateCancellation.Token.ThrowIfCancellationRequested();
        File.WriteAllText(this._stepsStartedFile, "started");
        await this._pipeline.RunAsync(runId, userCancellationToken, shutdownToken);
    }
}
#endif
