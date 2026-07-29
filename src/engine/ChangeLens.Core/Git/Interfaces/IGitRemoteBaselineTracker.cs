using ChangeLens.Core.Git.Models;
using ChangeLens.Core.Results.Models;

namespace ChangeLens.Core.Git.Interfaces;

/// <summary>
///     Defines detection and refresh of a cached remote-tracking comparison baseline against the server.
/// </summary>
/// <remarks>
///     <para>
///         Implementations are registered as scoped services. They serve one request and do not need to be thread-safe.
///     </para>
///     <para>
///         Detection never transfers objects and never modifies the repository. Refresh updates exactly one
///         remote-tracking reference and never touches local branches, the working tree, the index, or
///         <c>HEAD</c>.
///     </para>
/// </remarks>
public interface IGitRemoteBaselineTracker
{
    /// <summary>
    ///     Asynchronously checks whether a cached remote-tracking reference still matches the server's branch.
    /// </summary>
    /// <param name="path">The selected repository directory path.</param>
    /// <param name="target">The exact full cached remote-tracking reference.</param>
    /// <param name="cancellationToken">
    ///     A <see cref="CancellationToken" /> to observe while waiting for the task to complete.
    /// </param>
    /// <returns>
    ///     A task that represents the asynchronous operation. The task result contains the remote baseline state and
    ///     the remote revision when known.
    /// </returns>
    /// <exception cref="OperationCanceledException">
    ///     The <paramref name="cancellationToken" /> is canceled.
    /// </exception>
    Task<Result<RemoteBaselineCheckResult>> CheckAsync(
        string? path,
        string? target,
        CancellationToken cancellationToken);

    /// <summary>
    ///     Asynchronously fetches exactly the selected branch, moving its cached remote-tracking reference to match
    ///     the server.
    /// </summary>
    /// <param name="path">The selected repository directory path.</param>
    /// <param name="target">The exact full cached remote-tracking reference.</param>
    /// <param name="cancellationToken">
    ///     A <see cref="CancellationToken" /> to observe while waiting for the task to complete.
    /// </param>
    /// <returns>
    ///     A task that represents the asynchronous operation. The task result contains the new full object
    ///     identifier of the refreshed remote-tracking reference.
    /// </returns>
    /// <exception cref="OperationCanceledException">
    ///     The <paramref name="cancellationToken" /> is canceled.
    /// </exception>
    Task<Result<string>> RefreshAsync(
        string? path,
        string? target,
        CancellationToken cancellationToken);
}
