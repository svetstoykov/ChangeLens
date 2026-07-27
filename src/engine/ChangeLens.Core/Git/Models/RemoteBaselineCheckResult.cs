namespace ChangeLens.Core.Git.Models;

/// <summary>
///     Represents the outcome of one remote baseline check.
/// </summary>
/// <param name="State">The resulting remote baseline state.</param>
/// <param name="RemoteRevision">
///     The full object identifier the server advertised for the branch, or <see langword="null" /> when the state
///     is <see cref="RemoteBaselineState.NoRemote" /> and no network call was attempted.
/// </param>
public sealed record RemoteBaselineCheckResult(
    RemoteBaselineState State,
    string? RemoteRevision);
