namespace ChangeLens.Engine.Comparisons.Models;

/// <summary>
///     Represents a cached remote-tracking reference whose server branch has moved since the last fetch.
/// </summary>
/// <param name="RemoteRevision">The full object identifier the server currently advertises for the branch.</param>
internal sealed record MovedRemoteBaselineResult(string RemoteRevision) : RemoteBaselineResult;
