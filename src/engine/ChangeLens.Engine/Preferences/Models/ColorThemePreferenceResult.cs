namespace ChangeLens.Engine.Preferences.Models;

/// <summary>
///     Represents the optional explicit color-theme preference.
/// </summary>
/// <param name="ColorTheme">The explicit theme, or <see langword="null" /> for system theme.</param>
internal sealed record ColorThemePreferenceResult(ColorThemeResultValue? ColorTheme);
