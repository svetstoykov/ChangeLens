using System.Text.Json.Serialization;

namespace ChangeLens.Engine.Preferences.Models;

/// <summary>
///     Defines explicit color themes in the protocol.
/// </summary>
internal enum ColorThemeResultValue
{
    /// <summary>
    ///     The light theme.
    /// </summary>
    [JsonStringEnumMemberName("light")]
    Light,

    /// <summary>
    ///     The dark theme.
    /// </summary>
    [JsonStringEnumMemberName("dark")]
    Dark,
}
