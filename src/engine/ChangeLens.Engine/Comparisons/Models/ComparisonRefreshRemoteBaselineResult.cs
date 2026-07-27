namespace ChangeLens.Engine.Comparisons.Models;

/// <summary>
///     Represents the new revision of a refreshed remote-tracking reference in the engine protocol.
/// </summary>
/// <param name="RemoteRevision">The new full object identifier of the refreshed remote-tracking reference.</param>
internal sealed record ComparisonRefreshRemoteBaselineResult(string RemoteRevision);
