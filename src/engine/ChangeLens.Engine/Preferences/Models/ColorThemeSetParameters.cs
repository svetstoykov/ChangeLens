using System.Text.Json.Serialization;

namespace ChangeLens.Engine.Preferences.Models;

/// <summary>
///     Represents parameters for storing an explicit color-theme preference.
/// </summary>
internal sealed class ColorThemeSetParameters
{
    /// <summary>
    ///     Gets the explicit color theme.
    /// </summary>
    [JsonRequired]
    public ColorThemeResultValue ColorTheme { get; init; }
}
