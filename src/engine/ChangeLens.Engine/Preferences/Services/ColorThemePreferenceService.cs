using ChangeLens.Core.LocalState.Interfaces;
using ChangeLens.Core.LocalState.Models;
using ChangeLens.Core.Results.Models;

namespace ChangeLens.Engine.Preferences.Services;

/// <summary>
///     Provides the approved color-theme preference use cases.
/// </summary>
/// <param name="store">The durable color-theme preference store.</param>
internal sealed class ColorThemePreferenceService(IColorThemePreferenceStore store)
{
    /// <summary>
    ///     Asynchronously gets the explicit color-theme preference.
    /// </summary>
    /// <param name="cancellationToken">A <see cref="CancellationToken" /> to observe.</param>
    /// <returns>A task whose result contains the preference, or <see langword="null" />.</returns>
    internal Task<Result<ColorTheme?>> GetAsync(CancellationToken cancellationToken) =>
        store.GetAsync(cancellationToken);

    /// <summary>
    ///     Asynchronously stores the explicit color-theme preference.
    /// </summary>
    /// <param name="colorTheme">The theme to store.</param>
    /// <param name="cancellationToken">A <see cref="CancellationToken" /> to observe.</param>
    /// <returns>A task that represents the asynchronous operation.</returns>
    internal Task<Result> SetAsync(ColorTheme colorTheme, CancellationToken cancellationToken) =>
        store.SetAsync(colorTheme, cancellationToken);
}
