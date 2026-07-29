using ChangeLens.Engine.Protocol.Interfaces;
using ChangeLens.Engine.Protocol.Models;
using ChangeLens.Engine.Repositories.Constants;

namespace ChangeLens.Engine.IntegrationTests.Protocol.Support;

/// <summary>
///     Provides controlled behavior for the repository-open action.
/// </summary>
/// <param name="handleAsync">The behavior to invoke for a request.</param>
internal sealed class RepositoryOpenStubActionHandler(
    Func<EngineProtocolRequest, CancellationToken, Task<ProtocolResponse>> handleAsync) : IActionHandler
{
    /// <inheritdoc />
    public static string Action => RepositoryActionConstants.OpenAction;

    /// <inheritdoc />
    public Task<ProtocolResponse> HandleAsync(EngineProtocolRequest request, CancellationToken cancellationToken) =>
        handleAsync(request, cancellationToken);
}
