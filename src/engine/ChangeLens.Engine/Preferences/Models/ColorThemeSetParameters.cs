using System.Text.Json.Serialization;
using ChangeLens.Core.Preferences.Models;

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
