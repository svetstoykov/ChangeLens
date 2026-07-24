using System.Text.Json.Serialization;

namespace ChangeLens.Engine.Comparisons.Models;

/// <summary>
///     Defines comparison-target kinds in the engine protocol.
/// </summary>
internal enum ComparisonTargetKindResult
{
    /// <summary>
    ///     A local branch reference.
    /// </summary>
    [JsonStringEnumMemberName("local")]
    Local,

    /// <summary>
    ///     A cached remote-tracking branch reference.
    /// </summary>
    [JsonStringEnumMemberName("remoteTracking")]
    RemoteTracking,
}
