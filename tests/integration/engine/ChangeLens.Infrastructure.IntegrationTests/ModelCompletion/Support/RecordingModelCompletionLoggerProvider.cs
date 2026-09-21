using ChangeLens.Infrastructure.ModelCompletion.Services;
using Microsoft.Extensions.Logging;

namespace ChangeLens.Infrastructure.IntegrationTests.ModelCompletion.Support;

/// <summary>
///     Captures adapter and typed-HTTP-client log entries for security assertions.
/// </summary>
public sealed class RecordingModelCompletionLoggerProvider : ILoggerProvider
{
    private readonly RecordingModelCompletionLogger<OpenAiCompatibleModelCompletionClient> _logger = new();

    /// <summary>
    ///     Gets the recorded entries in emission order.
    /// </summary>
    public IReadOnlyList<(LogLevel Level, string Message)> Entries => this._logger.Entries;

    /// <inheritdoc />
    public ILogger CreateLogger(string categoryName) => this._logger;

    /// <inheritdoc />
    public void Dispose()
    {
    }
}
