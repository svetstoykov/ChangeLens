using ChangeLens.Core.LocalState.Interfaces;
using ChangeLens.Core.LocalState.Models;
using ChangeLens.Core.Results.Models;

namespace ChangeLens.Core.LocalState.Services;

/// <summary>
///     Provides the approved color-theme preference use cases.
/// </summary>
/// <param name="store">The durable color-theme preference store.</param>
public sealed class ColorThemePreferenceService(IColorThemePreferenceStore store) : IColorThemePreferenceService
{
    /// <inheritdoc />
    public Task<Result<ColorTheme?>> GetAsync(CancellationToken cancellationToken) =>
        store.GetAsync(cancellationToken);

    /// <inheritdoc />
    public Task<Result> SetAsync(ColorTheme colorTheme, CancellationToken cancellationToken) =>
        store.SetAsync(colorTheme, cancellationToken);
}
