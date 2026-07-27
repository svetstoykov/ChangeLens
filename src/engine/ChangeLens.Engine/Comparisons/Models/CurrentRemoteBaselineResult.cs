namespace ChangeLens.Engine.Comparisons.Models;

/// <summary>
///     Represents a cached remote-tracking reference that still matches the server's branch.
/// </summary>
/// <param name="RemoteRevision">The full object identifier the server advertised for the branch.</param>
internal sealed record CurrentRemoteBaselineResult(string RemoteRevision) : RemoteBaselineResult;
