using ChangeLens.Core.LocalState.Models;
using ChangeLens.Core.Results.Models;

namespace ChangeLens.Engine.Preferences.Interfaces;

/// <summary>
///     Defines color-theme preference use cases for engine actions.
/// </summary>
internal interface IColorThemePreferenceService
{
    /// <summary>
    ///     Asynchronously gets the explicit color-theme preference.
    /// </summary>
    /// <param name="cancellationToken">A <see cref="CancellationToken" /> to observe.</param>
    /// <returns>A task whose result contains the preference, or <see langword="null" />.</returns>
    Task<Result<ColorTheme?>> GetAsync(CancellationToken cancellationToken);

    /// <summary>
    ///     Asynchronously stores the explicit color-theme preference.
    /// </summary>
    /// <param name="colorTheme">The theme to store.</param>
    /// <param name="cancellationToken">A <see cref="CancellationToken" /> to observe.</param>
    /// <returns>A task that represents the asynchronous operation.</returns>
    Task<Result> SetAsync(ColorTheme colorTheme, CancellationToken cancellationToken);
}
