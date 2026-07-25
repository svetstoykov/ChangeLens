namespace ChangeLens.Core.Comparisons.Models;

/// <summary>
///     Defines the supported kinds of comparison target.
/// </summary>
public enum ComparisonTargetKind
{
    /// <summary>
    ///     A local branch reference.
    /// </summary>
    Local,

    /// <summary>
    ///     A cached remote-tracking branch reference.
    /// </summary>
    RemoteTracking,
}
