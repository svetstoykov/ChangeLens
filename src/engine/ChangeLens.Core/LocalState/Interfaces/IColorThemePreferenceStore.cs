using ChangeLens.Core.LocalState.Models;
using ChangeLens.Core.Results.Models;

namespace ChangeLens.Core.LocalState.Interfaces;

/// <summary>
///     Defines durable explicit color-theme preference operations.
/// </summary>
public interface IColorThemePreferenceStore
{
    /// <summary>
    ///     Asynchronously gets the explicit color-theme preference.
    /// </summary>
    /// <param name="cancellationToken">A <see cref="CancellationToken" /> to observe.</param>
    /// <returns>A task whose result contains the preference, or <see langword="null" /> for system theme.</returns>
    Task<Result<ColorTheme?>> GetAsync(CancellationToken cancellationToken);

    /// <summary>
    ///     Asynchronously stores an explicit color-theme preference.
    /// </summary>
    /// <param name="colorTheme">The explicit theme to store.</param>
    /// <param name="cancellationToken">A <see cref="CancellationToken" /> to observe.</param>
    /// <returns>A task that represents the asynchronous operation.</returns>
    Task<Result> SetAsync(ColorTheme colorTheme, CancellationToken cancellationToken);
}
