using ChangeLens.Core.LocalState.Interfaces;
using ChangeLens.Core.LocalState.Models;
using ChangeLens.Core.Results.Models;

namespace ChangeLens.Engine.IntegrationTests.Preferences.Handlers.Support;

/// <summary>
///     Provides one caller-selected stored theme and records whether persistence was reached.
/// </summary>
/// <param name="storedTheme">The theme returned by every read, or <see langword="null" />.</param>
internal sealed class StubColorThemePreferenceService(ColorTheme? storedTheme) : IColorThemePreferenceService
{
    /// <summary>
    ///     Gets a value indicating whether the handler called the persistence operation.
    /// </summary>
    internal bool SetCalled { get; private set; }

    /// <inheritdoc />
    public Task<Result<ColorTheme?>> GetAsync(CancellationToken cancellationToken) =>
        Task.FromResult(Result.Success<ColorTheme?>(storedTheme));

    /// <inheritdoc />
    public Task<Result> SetAsync(ColorTheme colorTheme, CancellationToken cancellationToken)
    {
        this.SetCalled = true;
        return Task.FromResult(Result.Success());
    }
}
